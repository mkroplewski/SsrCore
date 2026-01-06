import { createServer } from "vite";
import { fileURLToPath } from "url";

const __dirname = fileURLToPath(new URL(".", import.meta.url));

//This will serve the client code for .NET to proxy to, and provide ssrLoadModule for resolving the SSR entry point
export default async function startVite() {
  const vite = await createServer({
    root: __dirname,
    server: {
      middlewareMode: false,
    },
    appType: "custom",
  });

  const server = await vite.listen(0);
  const address = server.httpServer?.address();

  if (address === null || typeof address === "string" || address === undefined) {
    throw new Error("Failed to start Vite server");
  }
  console.log(`Vite running in-process on ${address.port}`);
  return { url: `http://localhost:${address.port}`, ssrLoadModule: vite.ssrLoadModule };
}
