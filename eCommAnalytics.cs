using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace eCommExpertsLogic
{
    public interface IShopifyAnalyticsService
    {
        Task<AnalyticsModel> GetStoreAnalytics(string shopDomain, string accessToken);
        Task<bool> ValidateCredentials(string shopDomain, string accessToken);
    }

    public class ShopifyAnalyticsService : IShopifyAnalyticsService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ShopifyAnalyticsService> _logger;
        private const int API_VERSION = 2024_01;

        public ShopifyAnalyticsService(
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            ILogger<ShopifyAnalyticsService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _logger = logger;
        }

        public async Task<bool> ValidateCredentials(string shopDomain, string accessToken)
        {
            try
            {
                using var client = CreateShopifyClient(shopDomain, accessToken);
                var response = await client.GetAsync("shop.json");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate credentials for shop {ShopDomain}", shopDomain);
                return false;
            }
        }

        public async Task<AnalyticsModel> GetStoreAnalytics(string shopDomain, string accessToken)
        {
            // Check cache first
            var cacheKey = $"analytics_{shopDomain}";
            if (_cache.TryGetValue(cacheKey, out AnalyticsModel cachedModel))
            {
                return cachedModel;
            }

            try
            {
                using var client = CreateShopifyClient(shopDomain, accessToken);
                var analyticsModel = new AnalyticsModel
                {
                    ShopData = await GetShopData(client, shopDomain),
                    ProductsData = await GetProductsData(client, shopDomain)
                };

                // Cache the results for 15 minutes
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));
                _cache.Set(cacheKey, analyticsModel, cacheOptions);

                return analyticsModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get analytics for shop {ShopDomain}", shopDomain);
                throw;
            }
        }

        private HttpClient CreateShopifyClient(string shopDomain, string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri($"https://{shopDomain}/admin/api/{API_VERSION}/");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-Shopify-Access-Token", accessToken);
            return client;
        }

        private async Task<ShopShop> GetShopData(HttpClient client, string shopDomain)
        {
            var shop = new ShopShop();
            
            // Get basic shop info
            var shopResponse = await client.GetAsync("shop.json");
            await EnsureSuccessResponse(shopResponse);
            var shopData = await GetJsonFromResponse<dynamic>(shopResponse);
            
            shop.Title = shopData.shop.name;
            shop.WebsiteURL = $"https://{shopDomain}";

            // Get metrics for all periods
            await Task.WhenAll(
                PopulateShopMetrics(client, shop, AnalyticsModel.AnalyticsPeriod.Last90Days),
                PopulateShopMetrics(client, shop, AnalyticsModel.AnalyticsPeriod.Last7Days),
                PopulateShopMetrics(client, shop, AnalyticsModel.AnalyticsPeriod.Yesterday)
            );

            return shop;
        }

        private async Task<Dictionary<string, ShopProduct>> GetProductsData(HttpClient client, string shopDomain)
        {
            var productsDict = new Dictionary<string, ShopProduct>();
            var hasMoreProducts = true;
            var pageInfo = string.Empty;
            
            while (hasMoreProducts)
            {
                var endpoint = "products.json?limit=250" + (!string.IsNullOrEmpty(pageInfo) ? $"&page_info={pageInfo}" : "");
                var productsResponse = await client.GetAsync(endpoint);
                await EnsureSuccessResponse(productsResponse);

                // Handle pagination
                if (productsResponse.Headers.Contains("Link"))
                {
                    var link = productsResponse.Headers.GetValues("Link").FirstOrDefault();
                    pageInfo = ExtractPageInfo(link);
                    hasMoreProducts = !string.IsNullOrEmpty(pageInfo);
                }
                else
                {
                    hasMoreProducts = false;
                }

                var productsData = await GetJsonFromResponse<dynamic>(productsResponse);
                foreach (var product in productsData.products)
                {
                    var shopProduct = await CreateShopProduct(client, product, shopDomain);
                    productsDict[shopProduct.Code] = shopProduct;
                }
            }

            return productsDict;
        }

        private async Task<ShopProduct> CreateShopProduct(HttpClient client, dynamic product, string shopDomain)
        {
            var shopProduct = new ShopProduct
            {
                ID = Guid.Parse(product.id.ToString()),
                Code = product.handle.ToString(),
                Title = product.title.ToString(),
                IsActive = product.status.ToString() == "active",
                ProductURL_Full = $"https://{shopDomain}/products/{product.handle}",
                ProductURL_Relative = $"/products/{product.handle}",
                Price = product.variants[0].price != null ? 
                    decimal.Parse(product.variants[0].price.ToString()) : 0,
                StockQty = product.variants[0].inventory_quantity != null ? 
                    int.Parse(product.variants[0].inventory_quantity.ToString()) : 0
            };

            if (product.image != null)
            {
                shopProduct.ImageURL_Small_Full = product.image.src.ToString();
            }

            // Get collections for categories
            await PopulateProductCategories(client, shopProduct, product.id.ToString());

            // Get metrics for all periods
            await Task.WhenAll(
                PopulateProductMetrics(client, shopProduct, AnalyticsModel.AnalyticsPeriod.Last90Days),
                PopulateProductMetrics(client, shopProduct, AnalyticsModel.AnalyticsPeriod.Last7Days),
                PopulateProductMetrics(client, shopProduct, AnalyticsModel.AnalyticsPeriod.Yesterday)
            );

            return shopProduct;
        }

        private async Task PopulateProductCategories(HttpClient client, ShopProduct product, string productId)
        {
            var collectionsEndpoint = $"collections.json?product_id={productId}";
            var collectionsResponse = await client.GetAsync(collectionsEndpoint);
            await EnsureSuccessResponse(collectionsResponse);
            var collectionsData = await GetJsonFromResponse<dynamic>(collectionsResponse);

            foreach (var collection in collectionsData.collections)
            {
                var category = new ShopCategory
                {
                    Id = collection.id.ToString(),
                    Title = collection.title.ToString(),
                    Handle = collection.handle.ToString()
                };

                // Assign category based on collection rules or tags
                // This is a simplified example - you might want to implement your own logic
                if (product.PrimaryCategory == null)
                {
                    product.PrimaryCategory = category;
                }
                else if (product.SecondaryCategory == null)
                {
                    product.SecondaryCategory = category;
                }
            }

            // Handle brand category from product vendor
            if (!string.IsNullOrEmpty(product.vendor?.ToString()))
            {
                product.BrandCategory = new ShopCategory
                {
                    Id = $"brand_{product.vendor}",
                    Title = product.vendor.ToString(),
                    Handle = product.vendor.ToString().ToLower().Replace(" ", "-")
                };
            }
        }

        private async Task EnsureSuccessResponse(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new ShopifyApiException(
                    $"Shopify API request failed with status {response.StatusCode}: {content}");
            }
        }

        // ... (other existing methods remain the same)
    }

    public class ShopifyApiException : Exception
    {
        public ShopifyApiException(string message) : base(message) { }
    }
}