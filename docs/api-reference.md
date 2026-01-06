# API Reference

## .NET API

### `Extensions.AddSsrCore(this WebApplicationBuilder builder, Action<SsrCoreOptions> configureOptions)`

Registers SsrCore services with the dependency injection container.

**Parameters:**

- `configureOptions`: A delegate to configure `SsrCoreOptions`.

### `Extensions.UseSsrCore(this WebApplication app)`

Adds the SsrCore middleware to the pipeline. This middleware intercepts requests generally intended for the SPA fallback and renders them on the server.

### `SsrCoreOptions`

Configuration options for SsrCore.

- **`RenderMode`**: Determines how the response body from Node.js is handled.

  - `RenderMode.String`: Works with standard rendering to string (for eg. ReactDOMServer.renderToString)
  - `RenderMode.WebReadableStream`: Treats the response body as a standard Web API `ReadableStream`. (Recommended for streaming)
  - `RenderMode.NodeReadableStream`: Treats the response body as a Node.js `stream.Readable`.

- **`EntryFunction`**: The name of the function in your server entry file. Defaults to `default`.

- **`FrontendPath`** The path to your frontend directory (containing vite.config.ts), relative from the project root, required for [Vite integration](./vite-integration.md). Defaults to `Frontend`.

## Node.js Interop Interface

### JavaScript Signature

The default export of your server bundle should be a function that takes a `Request` and returns a `Response` or `Promise<Response>`.

https://developer.mozilla.org/en-US/docs/Web/API/Request

https://developer.mozilla.org/en-US/docs/Web/API/Response

```typescript
export default async function (request: Request): Promise<Response> {
  // request is a standard Request object
  // return a standard Response object
}
```
