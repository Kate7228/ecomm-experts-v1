// app/root.jsx
import {
  Links,
  Meta,
  Outlet,
  Scripts,
  ScrollRestoration,
} from "@remix-run/react";
import { json } from "@remix-run/node"; // <-- add this

// Send a CSP that permits embedding your external iframe
export const loader = async () => {
  const csp = [
    "default-src 'self'",
    "base-uri 'self'",
    // Allow Shopify Admin to embed your app
    "frame-ancestors https://admin.shopify.com https://*.myshopify.com",
    // Allow YOUR app to embed the external page
    "frame-src https://admin.shopify.com https://*.myshopify.com https://api.ecommexperts.ai",
    // If the framed page does XHR/websockets to its own origin, allow it
    "connect-src 'self' https://*.shopify.com https://*.myshopify.com https://admin.shopify.com https://api.ecommexperts.ai",
    // Typical allowances for assets
    "img-src * data: blob:",
    "script-src 'self' 'unsafe-inline' 'unsafe-eval' https: http:",
    "style-src 'self' 'unsafe-inline' https: http:",
    "font-src https://cdn.shopify.com data:",
    "object-src 'none'",
    "upgrade-insecure-requests",
  ].join("; ");

  return json(null, { headers: { "Content-Security-Policy": csp } });
};

export default function App() {
  return (
    <html>
      <head>
        <meta charSet="utf-8" />
        <meta name="viewport" content="width=device-width,initial-scale=1" />
        <link rel="preconnect" href="https://cdn.shopify.com/" />
        <link
          rel="stylesheet"
          href="https://cdn.shopify.com/static/fonts/inter/v4/styles.css"
        />
        <Meta />
        <Links />
      </head>
      <body>
        <Outlet />
        <ScrollRestoration />
        <Scripts />
      </body>
    </html>
  );
}
