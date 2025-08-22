// app/routes/app._index.jsx
import { json } from "@remix-run/node";
import { useLoaderData } from "@remix-run/react";
import { authenticate } from "../shopify.server";

export const loader = async ({ request }) => {
  const { session } = await authenticate.admin(request);

  return json({
    token: session.accessToken,
    shop: session.shop,   // <-- this gives you "my-store.myshopify.com"
  });
};

export default function Index() {
  const { token, shop } = useLoaderData();

  return (
    <div
      style={{
        position: "fixed",
        inset: 0,
        margin: 0,
        padding: 0,
        width: "100%",
        height: "100%",
      }}
    >
      <iframe
        title="ECEC Experts"
        src={`https://api.ecommexperts.ai/shopify?ecexTkn=${encodeURIComponent(
          token ?? ""
        )}&shop=${encodeURIComponent(shop ?? "")}`}   // <-- append shop param
        style={{ border: "none", width: "100%", height: "100%", display: "block" }}
        allow="clipboard-read; clipboard-write;"
      />
    </div>
  );
}
