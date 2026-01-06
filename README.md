# SsrCore

**High-performance Server-Side Rendering (SSR) library for ASP.NET Core and Node.js.**

SsrCore bridges the gap between ASP.NET Core and Node.js, allowing you to run your JavaScript frontend (React, Vue, Svelte, etc.) within your .NET application while leveraging full streaming capabilities. It is designed to work with any Vite SSR bundle.

## Features

- **Streaming First**: Built from the ground up for streaming. Supports standard Web API `ReadableStream` piping directly to the ASP.NET Core `Response.Body`.
- **Web Standards**: Native support for standard `Request` and `Response` objects, making it compatible with modern frameworks and edge-ready logic.
- **Vite Compatible**: Designed to work seamlessly with any [Vite](https://vitejs.dev/) SSR bundle. Simply point SsrCore to your server entry point.
- **HMR Support**: During development SsrCore integrates with vite dev server, enabling features like Hot Module Replacement (HMR) and on-demand compilation of server-side code.
- **High Performance**: Optimized interop using shared memory and pinned buffers to minimize overhead between .NET and Node.js.
- **Framework Agnostic**: Works with any JavaScript framework (React, Vue, Svelte, Solid, etc.) that can run on Node.js and adheres to standard request/response patterns.

## Quick Start

### 1. Configure Services

In your ASP.NET Core `Program.cs`:

```csharp
using SsrCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddSsrCore(options =>
{
    // Choose your rendering mode
    options.RenderMode = RenderMode.WebReadableStream;
});
```

### 2. Configure Middleware

Add the middleware to your pipeline:

```csharp
var app = builder.Build();

// ... other middleware

app.UseSsrCore(); // Serves static files from /client AND handles SSR routes

app.Run();
```

## Documentation

- [Getting Started](./docs/getting-started.md)
- [API Reference](./docs/api-reference.md)
- [Vite Integration & HMR](./docs/vite-integration.md)

For detailed instructions and API reference, please visit the [Documentation](./docs).

## License

This project is licensed under the MIT License. See the [LICENSE](./LICENSE) file for details.
