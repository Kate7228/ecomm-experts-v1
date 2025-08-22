import { json } from "@remix-run/node";
import { authenticate } from "../shopify.server";
import { ShopifyAnalyticsService } from "../services/ShopifyAnalytics";

export const loader = async ({ request }) => {
  try {
    const { session } = await authenticate.admin(request);
    
    if (!session?.shop || !session?.accessToken) {
      console.error('Missing shop or access token:', { shop: session?.shop, hasToken: !!session?.accessToken });
      return json({ 
        error: 'Authentication error: Missing shop or access token',
        details: { shop: session?.shop, hasToken: !!session?.accessToken }
      }, { status: 401 });
    }

    console.log('Initializing analytics service for shop:', session.shop);
    const analyticsService = new ShopifyAnalyticsService(
      session.accessToken,
      session.shop
    );

    console.log('Fetching analytics data...');
    const analytics = await analyticsService.getAnalytics();
    console.log('Analytics data fetched successfully');
    
    return json(analytics);
  } catch (error) {
    console.error('Analytics error:', {
      message: error.message,
      stack: error.stack,
      shop: error.shop,
      statusCode: error.status
    });
    
    return json({ 
      error: error.message,
      details: {
        stack: error.stack,
        shop: error.shop,
        statusCode: error.status
      }
    }, { status: error.status || 500 });
  }
}; 