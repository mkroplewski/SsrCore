# Vite Integration

SsrCore provides seamless integration with Vite during development, enabling features like Hot Module Replacement (HMR) and on-demand compilation of server-side code.

## Development Experience

When running your ASP.NET Core application in the `Development` environment, SsrCore automatically enables the dev-server mode.

- **No Rebuilds**: Server-side code is compiled on-demand. There is no need to run a build step for every change.
- **HMR Support**: Development server works just like you would expect when using Vite, changes in code are reflected instantly.

## File Structure

To enable this integration, SsrCore expects a standard Vite project structure within your configured `FrontendPath` (defaults to `Frontend/`).

The key requirement is your server entry point:

```text
Frontend/
  src/
    entry-server.tsx
  vite.config.ts
  package.json
```

## Production vs Development

SsrCore handles the switching logic automatically based on the `ASPNETCORE_ENVIRONMENT` variable.

- **Development**: Proxies the client-side requests directly to Vite dev server and uses Vite for resolving the server entry.
- **Production**: Serves the optimized static assets and uses the pre-built server bundle for maximum performance.

## Current issues

### Rollup Native Bindings

When running the Vite dev server within a .NET host process, due to Rollup's native bindings an exception will be thrown. To resolve this, it is recommended to use the WebAssembly version of Rollup in your `package.json`:

```json
{
  "overrides": {
    "rollup": "npm:@rollup/wasm-node"
  }
}
```

### Vite Server Script Required

Currently, you need to provide a `vite-server.js` script in your Frontend directory to start the Vite dev server and expose the `ssrLoadModule` function. Create this file:

```javascript
// Frontend/vite-server.js
import { createServer } from "vite";
import { fileURLToPath } from "url";

const __dirname = fileURLToPath(new URL(".", import.meta.url));

// This will serve the client code for .NET to proxy to, and provide ssrLoadModule for resolving the SSR entry point
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
```

This script creates a Vite server instance and returns both the dev server URL (for proxying client requests) and the `ssrLoadModule` function (for loading SSR modules). The need for this script will likely be removed in a future version of SsrCore.
