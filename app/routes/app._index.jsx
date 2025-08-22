import { useEffect, useState } from "react";
import { json } from "@remix-run/node";
import { useLoaderData } from "@remix-run/react";
import { Page, Layout, Button, Banner, Card, Text, ProgressBar } from "@shopify/polaris";
import { authenticate } from "../shopify.server";

export const loader = async ({ request }) => {
  const { session } = await authenticate.admin(request);
  return json({
    token: session.accessToken,
    shop: session.shop
  });
};

export default function Index() {
  const { token, shop } = useLoaderData();
  const [analyticsData, setAnalyticsData] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [errorDetails, setErrorDetails] = useState(null);

  const testAnalytics = async () => {
    setLoading(true);
    setError(null);
    setErrorDetails(null);
    try {
      console.log('Fetching analytics...');
      const response = await fetch('/api/analytics');
      const data = await response.json();
      
      if (!response.ok) {
        console.error('Analytics API error:', data);
        throw new Error(data.error || 'Failed to fetch analytics');
      }
      
      console.log('Analytics data received:', data);
      setAnalyticsData(data);
    } catch (err) {
      console.error('Error in testAnalytics:', err);
      setError(err.message);
      if (err.details) {
        setErrorDetails(err.details);
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <Page fullWidth>
      <Layout>
        <Layout.Section>
          <Card sectioned>
            <div style={{ marginBottom: '1rem' }}>
              <Text variant="bodyMd" as="p">
                Current shop: {shop}
              </Text>
            </div>
            
            <Button
              primary
              loading={loading}
              onClick={testAnalytics}
            >
              Test Analytics
            </Button>

            {loading && (
              <div style={{ marginTop: '1rem' }}>
                <ProgressBar progress={100} size="small" />
                <Text variant="bodyMd" as="p" alignment="center">
                  Fetching analytics data...
                </Text>
              </div>
            )}

            {error && (
              <Banner status="critical" title="Error fetching analytics" onDismiss={() => setError(null)}>
                <p>{error}</p>
                {errorDetails && (
                  <div style={{ marginTop: '1rem' }}>
                    <Text variant="bodyMd" as="p">
                      Additional details:
                    </Text>
                    <pre style={{ whiteSpace: 'pre-wrap', marginTop: '0.5rem' }}>
                      {JSON.stringify(errorDetails, null, 2)}
                    </pre>
                  </div>
                )}
              </Banner>
            )}

            {analyticsData && (
              <div style={{ marginTop: '20px' }}>
                <Text variant="headingMd" as="h3">
                  Analytics Results
                </Text>
                <pre style={{ whiteSpace: 'pre-wrap', marginTop: '1rem', background: '#f4f6f8', padding: '1rem', borderRadius: '4px' }}>
                  {JSON.stringify(analyticsData, null, 2)}
                </pre>
              </div>
            )}
          </Card>
        </Layout.Section>

        <Layout.Section>
          <iframe
            src={`https://api.ecommexperts.ai/shopify?ecexTkn=${token}`}
            style={{
              width: "100%",
              height: "calc(100vh - 200px)", // Account for header and test section
              border: "none",
              margin: 0,
              padding: 0,
              display: "block"
            }}
            frameBorder="0"
          />
        </Layout.Section>
      </Layout>
    </Page>
  );
}
