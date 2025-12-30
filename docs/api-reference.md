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

- **`RenderMode`** (`RenderMode`): Determines how the response body from Node.js is handled.
    - `RenderMode.String`: Works with standard rendering to string (for eg. ReactDOMServer.renderToString)
    - `RenderMode.WebReadableStream`: Treats the response body as a standard Web API `ReadableStream`. (Recommended for streaming)
    - `RenderMode.NodeReadableStream`: Treats the response body as a Node.js `stream.Readable`.

## Node.js Interop Interface

Your JavaScript server entry point must implement the contract expected by `IEntryServer`.

```csharp
public interface IEntryServer
{
    public Task<IJsWebResponse> Default(JSValue request);
}
```

### JavaScript Signature

The default export of your server bundle should be an asynchronous function that takes a `Request` and returns a `Promise<Response>`.

```typescript
export default async function(request: Request): Promise<Response> {
    // request is a standard Request object
    // return a standard Response object
}
```

### `Request` and `Response`

SsrCore uses the standard global `Request` and `Response` objects available in modern Node.js environments.
