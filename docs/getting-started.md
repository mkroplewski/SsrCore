# Getting Started

This guide will help you set up SsrCore in your ASP.NET Core project.

## Prerequisites

- **.NET 8.0 SDK** or later.

## Installation

1.  Add the `SsrCore` project reference or NuGet package (when available) to your project.
2.  Ensure you have a **Vite** project setup with SSR enabled (`vite build --ssr`). SsrCore is designed to run the server bundle produced by Vite.

## Project Structure & Build Requirements

SsrCore has strict expectations for where files are located within your published output (i.e., inside `wwwroot`).

1.  **`wwwroot/client`**: Should contain your client-side assets (results of `vite build`).
2.  **`wwwroot/server`**: Should contain your server-side bundle (results of `vite build --ssr`).
3.  **`entry-server.mjs`**: The server bundle **MUST** be named `entry-server.mjs` inside the `server` folder and must be an ES Module (`.mjs`).

4.  **No External Dependencies**: The server bundle must be self-contained. You must configure Vite to bundle all dependencies so that `node_modules` are not required at runtime.

**Example Vite Config:**
Ensure your `vite.config.ts` (for server build) outputs to `wwwroot/server`, uses `.mjs` extension, and sets `ssr: { noExternal: true }`.

## Configuration

In your `Program.cs`, register SsrCore services and middleware.

```csharp
using SsrCore;

var builder = WebApplication.CreateBuilder(args);

// ... other services

// 1. Add SsrCore Services
builder.AddSsrCore(options =>
{
    // Configure how Node.js streams render
    // RenderMode.WebReadableStream is recommended for modern standards
    options.RenderMode = RenderMode.WebReadableStream;
});

var app = builder.Build();

// ... other middleware (StaticFiles, Routing, etc.)

// 2. Enable SsrCore Middleware
// This does two things:
// 1. Serves static files from 'wwwroot/client' (e.g. assets, js, css).
// 2. Handles fallback requests by invoking the SSR process for dynamic rendering.
app.UseSsrCore();

app.Run();
```

## JavaScript Entry Point

You need to export a function in your server entry point that matches the expected signature, taking a `Request` and returning a `Response` or `Promise<Response>`. The name of this function is configurable in [SsrCoreOptions](./api-reference.md#ssrcoreoptions).

https://developer.mozilla.org/en-US/docs/Web/API/Request

https://developer.mozilla.org/en-US/docs/Web/API/Response

**Example (TypeScript):**

```typescript
// entry-server.ts

export default async function handleRequest(request: Request): Promise<Response> {
  // 1. Process the request (e.g. routing)
  const url = new URL(request.url);

  // 2. Render your app to a stream
  const stream = await renderAppToStream(url);

  // 3. Return a standard Web Response
  return new Response(stream, {
    status: 200,
    headers: {
      "Content-Type": "text/html",
    },
  });
}
```
