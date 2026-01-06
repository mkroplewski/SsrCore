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
