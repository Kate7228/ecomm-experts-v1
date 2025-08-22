using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using eCommExpertsLogic;

namespace ShopifyAnalytics
{
    public class Category
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Handle { get; set; }
        public int ProductCount { get; set; }
        public Dictionary<AnalyticsModel.AnalyticsPeriod, CategoryAnalytics> Analytics { get; set; }
    }

    public class CategoryAnalytics
    {
        public decimal TotalRevenue { get; set; }
        public int TotalViews { get; set; }
        public int OrderCount { get; set; }
        public int QtySold { get; set; }
        public decimal ConversionRate { get; set; }
        public decimal AverageOrderValue { get; set; }
    }

    public class SessionData
    {
        public DateTime Date { get; set; }
        public int TotalSessions { get; set; }
        public int UniqueVisitors { get; set; }
        public decimal BounceRate { get; set; }
        public decimal AverageSessionDuration { get; set; }
        public Dictionary<string, int> PageViews { get; set; }
        public Dictionary<string, int> ProductViews { get; set; }
        public Dictionary<string, int> AddToCart { get; set; }
    }

    public class CustomerSegment
    {
        public string Name { get; set; }
        public int CustomerCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int RepeatPurchaseRate { get; set; }
        public List<string> TopProducts { get; set; }
    }

    public class ShopifyAnalyticsService
    {
        private readonly HttpClient _httpClient;
        private readonly string _accessToken;
        private readonly string _shopDomain;
        
        public ShopifyAnalyticsService(string accessToken, string shopDomain)
        {
            _accessToken = accessToken;
            _shopDomain = shopDomain;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("X-Shopify-Access-Token", accessToken);
        }

        public async Task<AnalyticsModel> GetAnalytics()
        {
            var analyticsModel = new AnalyticsModel
            {
                ShopData = await GetShopData(),
                ProductsData = await GetProductsData(),
                Categories = await GetCategories(),
                SessionData = await GetSessionData(),
                CustomerSegments = await GetCustomerSegments()
            };

            return analyticsModel;
        }

        private async Task<ShopShop> GetShopData()
        {
            try
            {
                // Get basic shop information
                var shopResponse = await _httpClient.GetAsync("https://api.shopify.com/admin/api/2024-01/shop.json");
                shopResponse.EnsureSuccessStatusCode();
                var shopContent = await shopResponse.Content.ReadAsStringAsync();
                var shopData = JsonSerializer.Deserialize<JsonElement>(shopContent).GetProperty("shop");

                var shop = new ShopShop
                {
                    Title = shopData.GetProperty("name").GetString(),
                    WebsiteURL = shopData.GetProperty("domain").GetString(),
                    Categories = new Dictionary<string, Category>(),
                    DailySessionData = new Dictionary<DateTime, SessionData>(),
                    CustomerSegments = new List<CustomerSegment>()
                };

                // Get analytics for different time periods
                await PopulateShopAnalytics(shop);

                return shop;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching shop data: {ex.Message}");
            }
        }

        private async Task PopulateShopAnalytics(ShopShop shop)
        {
            var ranges = new[]
            {
                (period: AnalyticsModel.AnalyticsPeriod.Last90Days, days: -90),
                (period: AnalyticsModel.AnalyticsPeriod.Last7Days, days: -7),
                (period: AnalyticsModel.AnalyticsPeriod.Yesterday, days: -1)
            };

            foreach (var (period, days) in ranges)
            {
                var startDate = DateTime.Now.AddDays(days);
                var endDate = period == AnalyticsModel.AnalyticsPeriod.Yesterday ? startDate.AddDays(1) : DateTime.Now;

                // Fetch orders
                var ordersResponse = await _httpClient.GetAsync(
                    $"https://api.shopify.com/admin/api/2024-01/orders.json?status=any&created_at_min={startDate:yyyy-MM-dd}&created_at_max={endDate:yyyy-MM-dd}");
                ordersResponse.EnsureSuccessStatusCode();
                var ordersData = await ordersResponse.Content.ReadAsStringAsync();
                var orders = JsonSerializer.Deserialize<JsonElement>(ordersData).GetProperty("orders");

                // Calculate metrics
                decimal revenue = 0;
                int orderCount = 0;
                int qtySold = 0;

                foreach (var order in orders.EnumerateArray())
                {
                    revenue += order.GetProperty("total_price").GetDecimal();
                    orderCount++;
                    
                    // Calculate total quantity sold
                    var lineItems = order.GetProperty("line_items");
                    foreach (var item in lineItems.EnumerateArray())
                    {
                        qtySold += item.GetProperty("quantity").GetInt32();
                    }
                }

                // Simulate sessions (in production, you'd get this from Shopify Analytics API)
                var sessions = orderCount * 20; // Assuming 5% conversion rate

                // Update the appropriate properties based on the period
                switch (period)
                {
                    case AnalyticsModel.AnalyticsPeriod.Last90Days:
                        shop.TotalRevenue_Last90Days = revenue;
                        shop.TotalSessions_Last90Days = sessions;
                        shop.OrderQty_Last90Days = orderCount;
                        shop.QtySold_Last90Days = qtySold;
                        shop.ConversionRate_Last90Days = orderCount / (decimal)sessions;
                        shop.AverageOrderValue_Last90Days = orderCount > 0 ? revenue / orderCount : 0;
                        break;

                    case AnalyticsModel.AnalyticsPeriod.Last7Days:
                        shop.TotalRevenue_Last7Days = revenue;
                        shop.TotalSessions_Last7Days = sessions;
                        shop.OrderQty_Last7Days = orderCount;
                        shop.QtySold_Last7Days = qtySold;
                        shop.ConversionRate_Last7Days = orderCount / (decimal)sessions;
                        shop.AverageOrderValue_Last7Days = orderCount > 0 ? revenue / orderCount : 0;
                        break;

                    case AnalyticsModel.AnalyticsPeriod.Yesterday:
                        shop.TotalRevenue_Yesterday = revenue;
                        shop.TotalSessions_Yesterday = sessions;
                        shop.OrderQty_Yesterday = orderCount;
                        shop.QtySold_Yesterday = qtySold;
                        shop.ConversionRate_Yesterday = orderCount / (decimal)sessions;
                        shop.AverageOrderValue_Yesterday = orderCount > 0 ? revenue / orderCount : 0;
                        break;
                }
            }
        }

        private async Task<Dictionary<string, ShopProduct>> GetProductsData()
        {
            var products = new Dictionary<string, ShopProduct>();

            try
            {
                // Get all products
                var productsResponse = await _httpClient.GetAsync("https://api.shopify.com/admin/api/2024-01/products.json");
                productsResponse.EnsureSuccessStatusCode();
                var productsContent = await productsResponse.Content.ReadAsStringAsync();
                var productsData = JsonSerializer.Deserialize<JsonElement>(productsContent).GetProperty("products");

                foreach (var productData in productsData.EnumerateArray())
                {
                    var product = new ShopProduct
                    {
                        ID = Guid.Parse(productData.GetProperty("id").GetString()),
                        Code = productData.GetProperty("handle").GetString(),
                        Title = productData.GetProperty("title").GetString(),
                        IsActive = productData.GetProperty("status").GetString() == "active",
                        ProductURL_Relative = $"/products/{productData.GetProperty("handle").GetString()}",
                        ProductURL_Full = $"https://{await GetShopDomain()}/products/{productData.GetProperty("handle").GetString()}",
                        Categories = new List<string>(),
                        ProductSessionData = new Dictionary<DateTime, SessionData>(),
                        RelatedProducts = new Dictionary<string, int>(),
                        CategoryPerformance = new Dictionary<string, decimal>()
                    };

                    // Get variants for price and inventory
                    var variants = productData.GetProperty("variants");
                    if (variants.GetArrayLength() > 0)
                    {
                        var firstVariant = variants[0];
                        product.Price = firstVariant.GetProperty("price").GetDecimal();
                        product.StockQty = firstVariant.GetProperty("inventory_quantity").GetInt32();
                    }

                    // Get primary image
                    var images = productData.GetProperty("images");
                    if (images.GetArrayLength() > 0)
                    {
                        product.ImageURL_Small_Full = images[0].GetProperty("src").GetString();
                    }

                    // Populate analytics for different time periods
                    await PopulateProductAnalytics(product);

                    products[product.Code] = product;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching products data: {ex.Message}");
            }

            return products;
        }

        private async Task PopulateProductAnalytics(ShopProduct product)
        {
            var ranges = new[]
            {
                (period: AnalyticsModel.AnalyticsPeriod.Last90Days, days: -90),
                (period: AnalyticsModel.AnalyticsPeriod.Last7Days, days: -7),
                (period: AnalyticsModel.AnalyticsPeriod.Yesterday, days: -1)
            };

            foreach (var (period, days) in ranges)
            {
                var startDate = DateTime.Now.AddDays(days);
                var endDate = period == AnalyticsModel.AnalyticsPeriod.Yesterday ? startDate.AddDays(1) : DateTime.Now;

                // Get orders containing this product
                var ordersResponse = await _httpClient.GetAsync(
                    $"https://api.shopify.com/admin/api/2024-01/orders.json?status=any&created_at_min={startDate:yyyy-MM-dd}&created_at_max={endDate:yyyy-MM-dd}");
                ordersResponse.EnsureSuccessStatusCode();
                var ordersData = await ordersResponse.Content.ReadAsStringAsync();
                var orders = JsonSerializer.Deserialize<JsonElement>(ordersData).GetProperty("orders");

                decimal revenue = 0;
                int qtySold = 0;
                int orderCount = 0;

                foreach (var order in orders.EnumerateArray())
                {
                    var lineItems = order.GetProperty("line_items");
                    foreach (var item in lineItems.EnumerateArray())
                    {
                        if (item.GetProperty("product_id").GetString() == product.ID.ToString())
                        {
                            var quantity = item.GetProperty("quantity").GetInt32();
                            var price = item.GetProperty("price").GetDecimal();
                            
                            revenue += price * quantity;
                            qtySold += quantity;
                            orderCount++;
                        }
                    }
                }

                // Get real view and add-to-cart data from Shopify Analytics API
                var analyticsResponse = await _httpClient.GetAsync(
                    $"https://{_shopDomain}/admin/api/2024-01/reports/product_views.json?product_id={product.ID}&created_at_min={startDate:yyyy-MM-dd}&created_at_max={endDate:yyyy-MM-dd}");
                analyticsResponse.EnsureSuccessStatusCode();
                var analyticsData = JsonSerializer.Deserialize<JsonElement>(await analyticsResponse.Content.ReadAsStringAsync());

                var views = 0;
                var addedToBasket = 0;

                foreach (var record in analyticsData.EnumerateArray())
                {
                    views += record.GetProperty("views").GetInt32();
                    addedToBasket += record.GetProperty("added_to_cart").GetInt32();

                    // Store daily session data
                    var date = record.GetProperty("date").GetDateTime();
                    product.ProductSessionData[date] = new SessionData
                    {
                        Date = date,
                        TotalSessions = record.GetProperty("sessions").GetInt32(),
                        UniqueVisitors = record.GetProperty("unique_visitors").GetInt32(),
                        BounceRate = record.GetProperty("bounce_rate").GetDecimal(),
                        AverageSessionDuration = record.GetProperty("average_time_on_page").GetDecimal(),
                        PageViews = new Dictionary<string, int> { { product.ProductURL_Relative, views } },
                        ProductViews = new Dictionary<string, int> { { product.Code, views } },
                        AddToCart = new Dictionary<string, int> { { product.Code, addedToBasket } }
                    };
                }

                // Get category data
                var collectionsResponse = await _httpClient.GetAsync(
                    $"https://{_shopDomain}/admin/api/2024-01/products/{product.ID}/collections.json");
                collectionsResponse.EnsureSuccessStatusCode();
                var collectionsData = JsonSerializer.Deserialize<JsonElement>(
                    await collectionsResponse.Content.ReadAsStringAsync()).GetProperty("collections");

                product.Categories.Clear();
                foreach (var collection in collectionsData.EnumerateArray())
                {
                    var categoryHandle = collection.GetProperty("handle").GetString();
                    product.Categories.Add(categoryHandle);
                    
                    // Update category performance
                    if (!product.CategoryPerformance.ContainsKey(categoryHandle))
                        product.CategoryPerformance[categoryHandle] = 0;
                    product.CategoryPerformance[categoryHandle] += revenue;
                }

                // Get related products based on order history
                var relatedResponse = await _httpClient.GetAsync(
                    $"https://{_shopDomain}/admin/api/2024-01/products/{product.ID}/recommendations.json");
                if (relatedResponse.IsSuccessStatusCode)
                {
                    var relatedData = JsonSerializer.Deserialize<JsonElement>(
                        await relatedResponse.Content.ReadAsStringAsync()).GetProperty("recommendations");
                    
                    product.RelatedProducts.Clear();
                    foreach (var related in relatedData.EnumerateArray())
                    {
                        var relatedId = related.GetProperty("product_id").GetString();
                        var score = related.GetProperty("score").GetInt32();
                        product.RelatedProducts[relatedId] = score;
                    }
                }

                // Update the appropriate properties based on the period
                switch (period)
                {
                    case AnalyticsModel.AnalyticsPeriod.Last90Days:
                        product.TotalRevenue_Last90Days = revenue;
                        product.TotalViews_Last90Days = views;
                        product.OrderQty_Last90Days = orderCount;
                        product.QtySold_Last90Days = qtySold;
                        product.NumberAddedToBasket_Last90Days = addedToBasket;
                        product.PercentAddToBasket_Last90Days = views > 0 ? (decimal)addedToBasket / views * 100 : 0;
                        product.PercentCompleteOrderAfterAddToBasket_Last90Days = addedToBasket > 0 ? (decimal)qtySold / addedToBasket * 100 : 0;
                        break;

                    case AnalyticsModel.AnalyticsPeriod.Last7Days:
                        product.TotalRevenue_Last7Days = revenue;
                        product.TotalViews_Last7Days = views;
                        product.OrderQty_Last7Days = orderCount;
                        product.QtySold_Last7Days = qtySold;
                        product.NumberAddedToBasket_Last7Days = addedToBasket;
                        product.PercentAddToBasket_Last7Days = views > 0 ? (decimal)addedToBasket / views * 100 : 0;
                        product.PercentCompleteOrderAfterAddToBasket_Last7Days = addedToBasket > 0 ? (decimal)qtySold / addedToBasket * 100 : 0;
                        break;

                    case AnalyticsModel.AnalyticsPeriod.Yesterday:
                        product.TotalRevenue_Yesterday = revenue;
                        product.TotalViews_Yesterday = views;
                        product.OrderQty_Yesterday = orderCount;
                        product.QtySold_Yesterday = qtySold;
                        product.NumberAddedToBasket_Yesterday = addedToBasket;
                        product.PercentAddToBasket_Yesterday = views > 0 ? (decimal)addedToBasket / views * 100 : 0;
                        product.PercentCompleteOrderAfterAddToBasket_Yesterday = addedToBasket > 0 ? (decimal)qtySold / addedToBasket * 100 : 0;
                        break;
                }
            }
        }

        private async Task<string> GetShopDomain()
        {
            var shopResponse = await _httpClient.GetAsync("https://api.shopify.com/admin/api/2024-01/shop.json");
            shopResponse.EnsureSuccessStatusCode();
            var shopContent = await shopResponse.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(shopContent).GetProperty("shop").GetProperty("domain").GetString();
        }

        private async Task<Dictionary<string, Category>> GetCategories()
        {
            var categories = new Dictionary<string, Category>();

            try
            {
                // Get all collections (Shopify's term for categories)
                var collectionsResponse = await _httpClient.GetAsync($"https://{_shopDomain}/admin/api/2024-01/custom_collections.json");
                collectionsResponse.EnsureSuccessStatusCode();
                var collectionsContent = await collectionsResponse.Content.ReadAsStringAsync();
                var collectionsData = JsonSerializer.Deserialize<JsonElement>(collectionsContent).GetProperty("custom_collections");

                foreach (var collectionData in collectionsData.EnumerateArray())
                {
                    var category = new Category
                    {
                        Id = collectionData.GetProperty("id").GetString(),
                        Title = collectionData.GetProperty("title").GetString(),
                        Handle = collectionData.GetProperty("handle").GetString(),
                        Analytics = new Dictionary<AnalyticsModel.AnalyticsPeriod, CategoryAnalytics>()
                    };

                    // Get products in this collection
                    var collectionProductsResponse = await _httpClient.GetAsync(
                        $"https://{_shopDomain}/admin/api/2024-01/collections/{category.Id}/products.json");
                    collectionProductsResponse.EnsureSuccessStatusCode();
                    var productsContent = await collectionProductsResponse.Content.ReadAsStringAsync();
                    var productsData = JsonSerializer.Deserialize<JsonElement>(productsContent).GetProperty("products");
                    
                    category.ProductCount = productsData.GetArrayLength();

                    // Calculate analytics for each time period
                    foreach (var period in Enum.GetValues<AnalyticsModel.AnalyticsPeriod>())
                    {
                        category.Analytics[period] = await GetCategoryAnalytics(category.Id, period);
                    }

                    categories[category.Handle] = category;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching categories data: {ex.Message}");
            }

            return categories;
        }

        private async Task<CategoryAnalytics> GetCategoryAnalytics(string categoryId, AnalyticsModel.AnalyticsPeriod period)
        {
            var (startDate, endDate) = GetDateRangeForPeriod(period);

            // Get analytics data from Shopify Analytics API
            var analyticsResponse = await _httpClient.GetAsync(
                $"https://{_shopDomain}/admin/api/2024-01/reports/sales.json?category_id={categoryId}&created_at_min={startDate:yyyy-MM-dd}&created_at_max={endDate:yyyy-MM-dd}");
            analyticsResponse.EnsureSuccessStatusCode();
            var analyticsData = JsonSerializer.Deserialize<JsonElement>(await analyticsResponse.Content.ReadAsStringAsync());

            var analytics = new CategoryAnalytics();
            
            // Parse analytics data and populate the CategoryAnalytics object
            foreach (var record in analyticsData.EnumerateArray())
            {
                analytics.TotalRevenue += record.GetProperty("total_sales").GetDecimal();
                analytics.OrderCount += record.GetProperty("orders").GetInt32();
                analytics.QtySold += record.GetProperty("units_sold").GetInt32();
                analytics.TotalViews += record.GetProperty("visits").GetInt32();
            }

            // Calculate derived metrics
            analytics.ConversionRate = analytics.TotalViews > 0 ? 
                (decimal)analytics.OrderCount / analytics.TotalViews : 0;
            analytics.AverageOrderValue = analytics.OrderCount > 0 ? 
                analytics.TotalRevenue / analytics.OrderCount : 0;

            return analytics;
        }

        private async Task<Dictionary<DateTime, SessionData>> GetSessionData()
        {
            var sessionData = new Dictionary<DateTime, SessionData>();

            try
            {
                var ranges = new[]
                {
                    (period: AnalyticsModel.AnalyticsPeriod.Last90Days, days: -90),
                    (period: AnalyticsModel.AnalyticsPeriod.Last7Days, days: -7),
                    (period: AnalyticsModel.AnalyticsPeriod.Yesterday, days: -1)
                };

                foreach (var (period, days) in ranges)
                {
                    var startDate = DateTime.Now.AddDays(days);
                    var endDate = period == AnalyticsModel.AnalyticsPeriod.Yesterday ? 
                        startDate.AddDays(1) : DateTime.Now;

                    // Get session data from Shopify Analytics API
                    var analyticsResponse = await _httpClient.GetAsync(
                        $"https://{_shopDomain}/admin/api/2024-01/reports/visitors.json?created_at_min={startDate:yyyy-MM-dd}&created_at_max={endDate:yyyy-MM-dd}");
                    analyticsResponse.EnsureSuccessStatusCode();
                    var analyticsData = JsonSerializer.Deserialize<JsonElement>(await analyticsResponse.Content.ReadAsStringAsync());

                    foreach (var record in analyticsData.EnumerateArray())
                    {
                        var date = record.GetProperty("date").GetDateTime();
                        sessionData[date] = new SessionData
                        {
                            Date = date,
                            TotalSessions = record.GetProperty("total_sessions").GetInt32(),
                            UniqueVisitors = record.GetProperty("unique_visitors").GetInt32(),
                            BounceRate = record.GetProperty("bounce_rate").GetDecimal(),
                            AverageSessionDuration = record.GetProperty("average_session_duration").GetDecimal(),
                            PageViews = GetPageViewsDictionary(record.GetProperty("page_views")),
                            ProductViews = GetPageViewsDictionary(record.GetProperty("product_views")),
                            AddToCart = GetPageViewsDictionary(record.GetProperty("add_to_cart"))
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching session data: {ex.Message}");
            }

            return sessionData;
        }

        private Dictionary<string, int> GetPageViewsDictionary(JsonElement element)
        {
            var dict = new Dictionary<string, int>();
            foreach (var item in element.EnumerateArray())
            {
                dict[item.GetProperty("path").GetString()] = item.GetProperty("views").GetInt32();
            }
            return dict;
        }

        private async Task<List<CustomerSegment>> GetCustomerSegments()
        {
            var segments = new List<CustomerSegment>();

            try
            {
                // Get customer segments from Shopify
                var customersResponse = await _httpClient.GetAsync(
                    $"https://{_shopDomain}/admin/api/2024-01/customers.json");
                customersResponse.EnsureSuccessStatusCode();
                var customersData = JsonSerializer.Deserialize<JsonElement>(
                    await customersResponse.Content.ReadAsStringAsync()).GetProperty("customers");

                // Process customers into segments
                var allCustomers = customersData.EnumerateArray().ToList();
                
                // VIP Customers (top 20% by revenue)
                var vipSegment = CreateCustomerSegment("VIP Customers", allCustomers
                    .OrderByDescending(c => c.GetProperty("total_spent").GetDecimal())
                    .Take(allCustomers.Count / 5));
                segments.Add(vipSegment);

                // New Customers (last 30 days)
                var thirtyDaysAgo = DateTime.Now.AddDays(-30);
                var newCustomers = allCustomers.Where(c => 
                    c.GetProperty("created_at").GetDateTime() >= thirtyDaysAgo);
                segments.Add(CreateCustomerSegment("New Customers", newCustomers));

                // Repeat Customers
                var repeatCustomers = allCustomers.Where(c => 
                    c.GetProperty("orders_count").GetInt32() > 1);
                segments.Add(CreateCustomerSegment("Repeat Customers", repeatCustomers));

                // At-Risk Customers (no purchase in last 90 days)
                var ninetyDaysAgo = DateTime.Now.AddDays(-90);
                var atRiskCustomers = allCustomers.Where(c => 
                    c.GetProperty("last_order_date").GetDateTime() <= ninetyDaysAgo);
                segments.Add(CreateCustomerSegment("At-Risk Customers", atRiskCustomers));
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching customer segments: {ex.Message}");
            }

            return segments;
        }

        private CustomerSegment CreateCustomerSegment(string name, IEnumerable<JsonElement> customers)
        {
            var customerList = customers.ToList();
            return new CustomerSegment
            {
                Name = name,
                CustomerCount = customerList.Count,
                TotalRevenue = customerList.Sum(c => c.GetProperty("total_spent").GetDecimal()),
                AverageOrderValue = customerList.Count > 0 ? 
                    customerList.Sum(c => c.GetProperty("total_spent").GetDecimal()) / customerList.Count : 0,
                RepeatPurchaseRate = customerList.Count > 0 ? 
                    (int)(customerList.Count(c => c.GetProperty("orders_count").GetInt32() > 1) / 
                    (decimal)customerList.Count * 100) : 0,
                TopProducts = GetTopProductsForCustomers(customerList)
            };
        }

        private List<string> GetTopProductsForCustomers(List<JsonElement> customers)
        {
            var productCounts = new Dictionary<string, int>();
            
            foreach (var customer in customers)
            {
                var ordersResponse = _httpClient.GetAsync(
                    $"https://{_shopDomain}/admin/api/2024-01/customers/{customer.GetProperty("id").GetString()}/orders.json")
                    .Result;
                
                if (ordersResponse.IsSuccessStatusCode)
                {
                    var orders = JsonSerializer.Deserialize<JsonElement>(
                        ordersResponse.Content.ReadAsStringAsync().Result).GetProperty("orders");
                    
                    foreach (var order in orders.EnumerateArray())
                    {
                        foreach (var item in order.GetProperty("line_items").EnumerateArray())
                        {
                            var productId = item.GetProperty("product_id").GetString();
                            if (!productCounts.ContainsKey(productId))
                                productCounts[productId] = 0;
                            productCounts[productId] += item.GetProperty("quantity").GetInt32();
                        }
                    }
                }
            }

            return productCounts.OrderByDescending(kvp => kvp.Value)
                .Take(5)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        private (DateTime startDate, DateTime endDate) GetDateRangeForPeriod(AnalyticsModel.AnalyticsPeriod period)
        {
            var endDate = DateTime.Now;
            var startDate = period switch
            {
                AnalyticsModel.AnalyticsPeriod.Last90Days => endDate.AddDays(-90),
                AnalyticsModel.AnalyticsPeriod.Last7Days => endDate.AddDays(-7),
                AnalyticsModel.AnalyticsPeriod.Yesterday => endDate.AddDays(-1),
                _ => throw new ArgumentException("Invalid period")
            };

            return (startDate, endDate);
        }
    }

    public class AnalyticsModel
    {
        public enum AnalyticsPeriod
        {
            Last90Days,
            Last7Days,
            Yesterday
        }

        public ShopShop ShopData { get; set; }
        public Dictionary<string, ShopProduct> ProductsData { get; set; }
        public Dictionary<string, Category> Categories { get; set; }
        public Dictionary<DateTime, SessionData> SessionData { get; set; }
        public List<CustomerSegment> CustomerSegments { get; set; }
    }

    public class ShopShop
    {
        public string Title { get; set; }
        public string WebsiteURL { get; set; }

        // Revenue metrics
        public decimal TotalRevenue_Last90Days { get; set; }
        public decimal TotalRevenue_Last7Days { get; set; }
        public decimal TotalRevenue_Yesterday { get; set; }

        // Session metrics
        public int TotalSessions_Last90Days { get; set; }
        public int TotalSessions_Last7Days { get; set; }
        public int TotalSessions_Yesterday { get; set; }

        // Order metrics
        public int OrderQty_Last90Days { get; set; }
        public int OrderQty_Last7Days { get; set; }
        public int OrderQty_Yesterday { get; set; }

        // Product metrics
        public int QtySold_Last90Days { get; set; }
        public int QtySold_Last7Days { get; set; }
        public int QtySold_Yesterday { get; set; }

        // Conversion metrics
        public decimal ConversionRate_Last90Days { get; set; }
        public decimal ConversionRate_Last7Days { get; set; }
        public decimal ConversionRate_Yesterday { get; set; }

        // Average order value
        public decimal AverageOrderValue_Last90Days { get; set; }
        public decimal AverageOrderValue_Last7Days { get; set; }
        public decimal AverageOrderValue_Yesterday { get; set; }

        // New data structures
        public Dictionary<string, Category> Categories { get; set; }
        public Dictionary<DateTime, SessionData> DailySessionData { get; set; }
        public List<CustomerSegment> CustomerSegments { get; set; }
    }

    public class ShopProduct
    {
        public Guid ID { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
        public bool IsActive { get; set; }
        public decimal Price { get; set; }
        public int StockQty { get; set; }
        public string ProductURL_Relative { get; set; }
        public string ProductURL_Full { get; set; }
        public string ImageURL_Small_Full { get; set; }

        // Revenue metrics
        public decimal TotalRevenue_Last90Days { get; set; }
        public decimal TotalRevenue_Last7Days { get; set; }
        public decimal TotalRevenue_Yesterday { get; set; }

        // View metrics
        public int TotalViews_Last90Days { get; set; }
        public int TotalViews_Last7Days { get; set; }
        public int TotalViews_Yesterday { get; set; }

        // Order metrics
        public int OrderQty_Last90Days { get; set; }
        public int OrderQty_Last7Days { get; set; }
        public int OrderQty_Yesterday { get; set; }

        // Product metrics
        public int QtySold_Last90Days { get; set; }
        public int QtySold_Last7Days { get; set; }
        public int QtySold_Yesterday { get; set; }

        // Add to basket metrics
        public int NumberAddedToBasket_Last90Days { get; set; }
        public int NumberAddedToBasket_Last7Days { get; set; }
        public int NumberAddedToBasket_Yesterday { get; set; }

        // Conversion metrics
        public decimal PercentAddToBasket_Last90Days { get; set; }
        public decimal PercentAddToBasket_Last7Days { get; set; }
        public decimal PercentAddToBasket_Yesterday { get; set; }

        public decimal PercentCompleteOrderAfterAddToBasket_Last90Days { get; set; }
        public decimal PercentCompleteOrderAfterAddToBasket_Last7Days { get; set; }
        public decimal PercentCompleteOrderAfterAddToBasket_Yesterday { get; set; }

        // New data structures
        public List<string> Categories { get; set; }
        public Dictionary<DateTime, SessionData> ProductSessionData { get; set; }
        public Dictionary<string, int> RelatedProducts { get; set; }
        public Dictionary<string, decimal> CategoryPerformance { get; set; }
    }
} 