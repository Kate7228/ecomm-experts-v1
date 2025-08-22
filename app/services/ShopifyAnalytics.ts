interface CategoryAnalytics {
  totalRevenue: number;
  totalViews: number;
  orderCount: number;
  qtySold: number;
  conversionRate: number;
  averageOrderValue: number;
}

interface Category {
  id: string;
  title: string;
  handle: string;
  productCount: number;
  analytics: Record<string, CategoryAnalytics>;
}

interface SessionData {
  date: string;
  totalSessions: number;
  uniqueVisitors: number;
  bounceRate: number;
  averageSessionDuration: number;
  pageViews: Record<string, number>;
  productViews: Record<string, number>;
  addToCart: Record<string, number>;
}

interface CustomerSegment {
  name: string;
  customerCount: number;
  totalRevenue: number;
  averageOrderValue: number;
  repeatPurchaseRate: number;
  topProducts: string[];
}

interface ShopData {
  title: string;
  websiteURL: string;
  totalRevenue_Last90Days: number;
  totalRevenue_Last7Days: number;
  totalRevenue_Yesterday: number;
  totalSessions_Last90Days: number;
  totalSessions_Last7Days: number;
  totalSessions_Yesterday: number;
  orderQty_Last90Days: number;
  orderQty_Last7Days: number;
  orderQty_Yesterday: number;
  qtySold_Last90Days: number;
  qtySold_Last7Days: number;
  qtySold_Yesterday: number;
  conversionRate_Last90Days: number;
  conversionRate_Last7Days: number;
  conversionRate_Yesterday: number;
  averageOrderValue_Last90Days: number;
  averageOrderValue_Last7Days: number;
  averageOrderValue_Yesterday: number;
  categories: Record<string, Category>;
  dailySessionData: Record<string, SessionData>;
  customerSegments: CustomerSegment[];
}

interface ProductData {
  id: string;
  code: string;
  title: string;
  isActive: boolean;
  price: number;
  stockQty: number;
  productURL_Relative: string;
  productURL_Full: string;
  imageURL_Small_Full: string;
  totalRevenue_Last90Days: number;
  totalRevenue_Last7Days: number;
  totalRevenue_Yesterday: number;
  totalViews_Last90Days: number;
  totalViews_Last7Days: number;
  totalViews_Yesterday: number;
  orderQty_Last90Days: number;
  orderQty_Last7Days: number;
  orderQty_Yesterday: number;
  qtySold_Last90Days: number;
  qtySold_Last7Days: number;
  qtySold_Yesterday: number;
  numberAddedToBasket_Last90Days: number;
  numberAddedToBasket_Last7Days: number;
  numberAddedToBasket_Yesterday: number;
  percentAddToBasket_Last90Days: number;
  percentAddToBasket_Last7Days: number;
  percentAddToBasket_Yesterday: number;
  percentCompleteOrderAfterAddToBasket_Last90Days: number;
  percentCompleteOrderAfterAddToBasket_Last7Days: number;
  percentCompleteOrderAfterAddToBasket_Yesterday: number;
  categories: string[];
  productSessionData: Record<string, SessionData>;
  relatedProducts: Record<string, number>;
  categoryPerformance: Record<string, number>;
}

interface AnalyticsData {
  shopData: ShopData;
  productsData: Record<string, ProductData>;
  categories: Record<string, Category>;
  sessionData: Record<string, SessionData>;
  customerSegments: CustomerSegment[];
}

export class ShopifyAnalyticsService {
  private accessToken: string;
  private shopDomain: string;

  constructor(accessToken: string, shopDomain: string) {
    this.accessToken = accessToken;
    this.shopDomain = shopDomain;
  }

  async getAnalytics(): Promise<AnalyticsData> {
    try {
      // We don't need to fetch the reports endpoint since we're making specific API calls
      // in each transform method
      return await this.transformAnalyticsData({});
    } catch (error) {
      console.error('Error fetching analytics:', error);
      throw error;
    }
  }

  private async transformAnalyticsData(data: any): Promise<AnalyticsData> {
    // Transform the raw Shopify data into our analytics format
    const [
      shopData,
      productsData,
      categories,
      sessionData,
      customerSegments
    ] = await Promise.all([
      this.transformShopData(data),
      this.transformProductsData(data),
      this.transformCategoriesData(data),
      this.transformSessionData(data),
      this.transformCustomerSegments(data)
    ]);

    return {
      shopData,
      productsData,
      categories,
      sessionData,
      customerSegments
    };
  }

  private async transformShopData(data: any): Promise<ShopData> {
    try {
      // Get shop information
      const shopResponse = await fetch(`https://${this.shopDomain}/admin/api/2024-01/shop.json`, {
        headers: {
          'X-Shopify-Access-Token': this.accessToken,
          'Content-Type': 'application/json',
        },
      });

      if (!shopResponse.ok) {
        throw new Error(`Failed to fetch shop data: ${shopResponse.statusText}`);
      }

      const shopData = await shopResponse.json();
      const shop = shopData.shop;

      // Get orders for different time periods
      const periods = [
        { name: 'Last90Days', days: -90 },
        { name: 'Last7Days', days: -7 },
        { name: 'Yesterday', days: -1 }
      ];

      const metrics: any = {};

      for (const period of periods) {
        const startDate = new Date();
        startDate.setDate(startDate.getDate() + period.days);
        const endDate = period.name === 'Yesterday' ? new Date(startDate.getTime() + 86400000) : new Date();

        const ordersResponse = await fetch(
          `https://${this.shopDomain}/admin/api/2024-01/orders.json?status=any&created_at_min=${startDate.toISOString()}&created_at_max=${endDate.toISOString()}`,
          {
            headers: {
              'X-Shopify-Access-Token': this.accessToken,
              'Content-Type': 'application/json',
            },
          }
        );

        if (!ordersResponse.ok) {
          console.warn(`Failed to fetch orders for ${period.name}:`, ordersResponse.statusText);
          continue;
        }

        const ordersData = await ordersResponse.json();
        const orders = ordersData.orders || [];

        let revenue = 0;
        let orderCount = 0;
        let qtySold = 0;

        for (const order of orders) {
          revenue += parseFloat(order.total_price || '0');
          orderCount++;
          for (const item of (order.line_items || [])) {
            qtySold += item.quantity || 0;
          }
        }

        // Simulate sessions (in production, you'd get this from Analytics API)
        const sessions = Math.max(orderCount * 20, 1);

        metrics[`totalRevenue_${period.name}`] = revenue;
        metrics[`totalSessions_${period.name}`] = sessions;
        metrics[`orderQty_${period.name}`] = orderCount;
        metrics[`qtySold_${period.name}`] = qtySold;
        metrics[`conversionRate_${period.name}`] = orderCount / sessions;
        metrics[`averageOrderValue_${period.name}`] = orderCount > 0 ? revenue / orderCount : 0;
      }

      return {
        title: shop.name || 'Unknown Shop',
        websiteURL: shop.domain || this.shopDomain,
        ...metrics,
        categories: {},
        dailySessionData: {},
        customerSegments: []
      };
    } catch (error) {
      console.error('Error in transformShopData:', error);
      throw error;
    }
  }

  private async transformProductsData(data: any): Promise<Record<string, ProductData>> {
    try {
      const productsResponse = await fetch(`https://${this.shopDomain}/admin/api/2024-01/products.json`, {
        headers: {
          'X-Shopify-Access-Token': this.accessToken,
          'Content-Type': 'application/json',
        },
      });

      if (!productsResponse.ok) {
        console.warn('Failed to fetch products:', productsResponse.statusText);
        return {};
      }

      const productsData = await productsResponse.json();
      const products: Record<string, ProductData> = {};

      for (const product of (productsData.products || [])) {
        const variant = (product.variants || [])[0] || {};
        const image = (product.images || [])[0] || {};
        
        products[product.handle] = {
          id: product.id || '',
          code: product.handle || '',
          title: product.title || '',
          isActive: product.status === 'active',
          price: parseFloat(variant.price || '0'),
          stockQty: variant.inventory_quantity || 0,
          productURL_Relative: `/products/${product.handle || ''}`,
          productURL_Full: `https://${this.shopDomain}/products/${product.handle || ''}`,
          imageURL_Small_Full: image.src || '',
          totalRevenue_Last90Days: 0,
          totalRevenue_Last7Days: 0,
          totalRevenue_Yesterday: 0,
          totalViews_Last90Days: 0,
          totalViews_Last7Days: 0,
          totalViews_Yesterday: 0,
          orderQty_Last90Days: 0,
          orderQty_Last7Days: 0,
          orderQty_Yesterday: 0,
          qtySold_Last90Days: 0,
          qtySold_Last7Days: 0,
          qtySold_Yesterday: 0,
          numberAddedToBasket_Last90Days: 0,
          numberAddedToBasket_Last7Days: 0,
          numberAddedToBasket_Yesterday: 0,
          percentAddToBasket_Last90Days: 0,
          percentAddToBasket_Last7Days: 0,
          percentAddToBasket_Yesterday: 0,
          percentCompleteOrderAfterAddToBasket_Last90Days: 0,
          percentCompleteOrderAfterAddToBasket_Last7Days: 0,
          percentCompleteOrderAfterAddToBasket_Yesterday: 0,
          categories: [],
          productSessionData: {},
          relatedProducts: {},
          categoryPerformance: {}
        };
      }

      return products;
    } catch (error) {
      console.error('Error in transformProductsData:', error);
      return {};
    }
  }

  private async transformCategoriesData(data: any): Promise<Record<string, Category>> {
    try {
      const collectionsResponse = await fetch(`https://${this.shopDomain}/admin/api/2024-01/custom_collections.json`, {
        headers: {
          'X-Shopify-Access-Token': this.accessToken,
          'Content-Type': 'application/json',
        },
      });

      if (!collectionsResponse.ok) {
        console.warn('Failed to fetch collections:', collectionsResponse.statusText);
        return {};
      }

      const collectionsData = await collectionsResponse.json();
      const categories: Record<string, Category> = {};

      for (const collection of (collectionsData.custom_collections || [])) {
        categories[collection.handle] = {
          id: collection.id || '',
          title: collection.title || '',
          handle: collection.handle || '',
          productCount: 0,
          analytics: {}
        };
      }

      return categories;
    } catch (error) {
      console.error('Error in transformCategoriesData:', error);
      return {};
    }
  }

  private async transformSessionData(data: any): Promise<Record<string, SessionData>> {
    try {
      // In a real implementation, you'd get this from Shopify's Analytics API
      // For now, we'll return simulated data for the last 90 days
      const sessionData: Record<string, SessionData> = {};
      const startDate = new Date();
      startDate.setDate(startDate.getDate() - 90);

      for (let d = new Date(startDate); d <= new Date(); d.setDate(d.getDate() + 1)) {
        const dateStr = d.toISOString().split('T')[0];
        sessionData[dateStr] = {
          date: dateStr,
          totalSessions: Math.floor(Math.random() * 100) + 50,
          uniqueVisitors: Math.floor(Math.random() * 80) + 40,
          bounceRate: Math.random() * 0.4 + 0.2,
          averageSessionDuration: Math.random() * 300 + 120,
          pageViews: {},
          productViews: {},
          addToCart: {}
        };
      }

      return sessionData;
    } catch (error) {
      console.error('Error in transformSessionData:', error);
      return {};
    }
  }

  private async transformCustomerSegments(data: any): Promise<CustomerSegment[]> {
    try {
      const customersResponse = await fetch(`https://${this.shopDomain}/admin/api/2024-01/customers.json`, {
        headers: {
          'X-Shopify-Access-Token': this.accessToken,
          'Content-Type': 'application/json',
        },
      });

      if (!customersResponse.ok) {
        console.warn('Failed to fetch customers:', customersResponse.statusText);
        return [];
      }

      const customersData = await customersResponse.json();
      const customers = customersData.customers || [];

      if (customers.length === 0) {
        console.log('No customers found');
        return [];
      }

      // Sort customers by total spent
      const sortedCustomers = customers
        .filter((c: any) => c && typeof c.total_spent === 'string')
        .sort((a: any, b: any) => 
          parseFloat(b.total_spent || '0') - parseFloat(a.total_spent || '0')
        );

      // VIP Customers (top 20%)
      const vipCount = Math.max(1, Math.floor(sortedCustomers.length * 0.2));
      const vipCustomers = sortedCustomers.slice(0, vipCount);

      // New Customers (last 30 days)
      const thirtyDaysAgo = new Date();
      thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30);
      const newCustomers = customers.filter((c: any) => 
        c && c.created_at && new Date(c.created_at) >= thirtyDaysAgo
      );

      // Repeat Customers
      const repeatCustomers = customers.filter((c: any) => 
        c && c.orders_count && parseInt(c.orders_count) > 1
      );

      const createSegment = (name: string, segmentCustomers: any[]) => ({
        name,
        customerCount: segmentCustomers.length,
        totalRevenue: segmentCustomers.reduce((sum: number, c: any) => 
          sum + parseFloat(c.total_spent || '0'), 0
        ),
        averageOrderValue: segmentCustomers.length > 0 ? 
          segmentCustomers.reduce((sum: number, c: any) => 
            sum + parseFloat(c.total_spent || '0'), 0
          ) / segmentCustomers.length : 0,
        repeatPurchaseRate: segmentCustomers.length > 0 ? 
          (segmentCustomers.filter((c: any) => 
            c && c.orders_count && parseInt(c.orders_count) > 1
          ).length / segmentCustomers.length * 100) : 0,
        topProducts: []
      });

      return [
        createSegment('VIP Customers', vipCustomers),
        createSegment('New Customers', newCustomers),
        createSegment('Repeat Customers', repeatCustomers)
      ];
    } catch (error) {
      console.error('Error in transformCustomerSegments:', error);
      return [];
    }
  }
} 