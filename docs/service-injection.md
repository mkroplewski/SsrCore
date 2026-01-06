# Service Injection

Service Injection enables you to expose .NET services directly to your JavaScript environment during server-side rendering. This allows your frontend code to call backend services directly during the render phase, eliminating the need for separate API endpoints or complex data fetching patterns, and boosting performance.

## Table of Contents

- [Overview](#overview)
- [How It Works](#how-it-works)
- [Quick Start](#quick-start)
- [Detailed Setup](#detailed-setup)
  - [1. Define a Service Interface](#1-define-a-service-interface)
  - [2. Implement the Service](#2-implement-the-service)
  - [3. Register the Service](#3-register-the-service)
  - [4. Access the Service in TypeScript](#4-access-the-service-in-typescript)
- [Type Safety](#type-safety)
- [Best Practices](#best-practices)
- [Limitations](#limitations)
- [Advanced Usage](#advanced-usage)
- [Troubleshooting](#troubleshooting)

## Overview

Service Injection solves a common challenge in SSR applications: how to access backend data and business logic during the server-side render without making HTTP requests or duplicating logic.

**Key Benefits:**

- **Zero API overhead**: Call .NET methods directly from your SSR code
- **Type-safe**: Automatically generates TypeScript definitions
- **Scoped per request**: Services respect ASP.NET Core dependency injection scopes
- **Async/Await support**: Full support for asynchronous operations
- **Clean architecture**: Keep your business logic in .NET, access it seamlessly from JS

## How It Works

```
┌─────────────────────────────────────────────────────────────┐
│                   HTTP Request Arrives                       │
└────────────────────────────────┬────────────────────────────┘
                                 │
                    ┌────────────▼─────────────┐
                    │  ASP.NET Core Pipeline   │
                    │  (Middleware, DI, etc.)  │
                    └────────────┬─────────────┘
                                 │
                    ┌────────────▼─────────────┐
                    │    SsrCore Middleware    │
                    │  Creates Node.js Context │
                    └────────────┬─────────────┘
                                 │
        ┌────────────────────────┼────────────────────────┐
        │                        │                        │
┌───────▼────────┐    ┌──────────▼──────────┐   ┌────────▼────────┐
│ Inject Service │───▶│ Node.js SSR Runtime │◀──│ Your React/Vue  │
│ Proxies to     │    │  (globalThis scope)  │   │ Components Call │
│ globalThis     │    └─────────────────────┘   │ globalThis.xyz  │
└────────────────┘                               └─────────────────┘
        │
        │  Service calls are marshalled via node-api-dotnet
        │
┌───────▼─────────────────────────────────────────────────────┐
│            .NET Service Implementation                       │
│   (Executes in ASP.NET Core thread with full DI support)    │
└──────────────────────────────────────────────────────────────┘
```

During each request:

1. SsrCore middleware resolves your registered services from the current request's DI scope
2. Services are wrapped as JavaScript proxies
3. These proxies are injected into `globalThis` in the Node.js SSR environment
4. Your JS code calls these services as if they were native JavaScript
5. Calls are marshalled back to .NET, executed, and results returned
6. After rendering completes, services are cleaned up from `globalThis`

## Quick Start

**Step 1:** Define and export a service interface with `[JSExport]` attribute, this is necessary for typescript codegen. It has to be an interface, not a class.

```csharp
// IGreeterService.cs
using Microsoft.JavaScript.NodeApi;

[JSExport("greeterService")]
public interface IGreeterService
{
    Task<string> GreetAsync(string name);
}
```

**Step 2:** Implement the service:

```csharp
// GreeterService.cs
public class GreeterService : IGreeterService
{
    public async Task<string> GreetAsync(string name)
    {
        await Task.Delay(100); // Simulate work
        return $"Hello, {name}!";
    }
}
```

**Step 3:** Register in `Program.cs`:

```csharp
builder.Services.AddScoped<IGreeterService, GreeterService>();

builder.AddSsrCore(options =>
{
    //Make sure the name passed here matches the one from `[JSExport]` attribute from step 1.
    //Otherwise the type definitons won't match.
    options.Services.Inject<IGreeterService>("greeterService");
});
```

**Step 4:** Create global type declarations in `src/global.d.ts`:

```typescript
import generated from "./_generated/api";

declare global {
  var greeterService: generated.greeterService;
}
```

**Step 5:** Use in your SSR code (Tanstack Router example):

```tsx
// src/routes/index.tsx
export const Route = createFileRoute("/")({
  loader: () => {
    return {
      greeting: typeof window === "undefined" ? globalThis.greeterService.greetAsync("World") : Promise.resolve("Client-side"),
    };
  },
  component: IndexComponent,
});
```

## Type Safety

Service Injection provides end-to-end type safety:

[**JS/.NET Type mappings**](https://microsoft.github.io/node-api-dotnet/reference/js-dotnet-types.html)

### Example Type Generation

**C# Interface:**

```csharp
using Microsoft.JavaScript.NodeApi;

[JSExport("userService")]
public interface IUserService
{
    Task<IUser> GetUserAsync(int id);
    Task<List<IUser>> GetAllUsersAsync();
    Task<bool> UpdateUserAsync(int id, IUserUpdate update);
}
[JSExport]
public interface IUser
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
}
[JSExport]
public interface IUserUpdate
{
    public string? Name { get; set; }
    public string? Email { get; set; }
}
```

**Generated TypeScript (`_generated/api.d.ts`):**

```typescript
export interface userService {
  getUserAsync(id: number): Promise<User>;
  getAllUsersAsync(): Promise<User[]>;
  updateUserAsync(id: number, update: UserUpdate): Promise<boolean>;
}

export interface User {
  id: number;
  name: string;
  createdAt: Date;
}

export interface UserUpdate {
  name?: string;
  email?: string;
}
```

**Important Note on Type Generation**

In the example above, all data models are defined as interfaces rather than classes. This is a requirement for proper TypeScript type generation.

**Why interfaces?** Since SsrCore only generates TypeScript declaration files (`.d.ts`), not implementation files, classes cannot be instantiated from type definitions alone. You can use classes as return types from your service methods (they'll be marshalled as JS classes which is less elegant), but method parameters must be interfaces or types supported by node-api-dotnet.

## Limitations

### 1. **Server-Side Only**

Services are **only available during SSR**, not on the client. After hydration, `globalThis.myService` will be `undefined`.

**Solution:** Implement separate client-side data fetching:

```typescript
export const Route = createFileRoute("/data")({
  loader: async () => {
    if (typeof window === "undefined") {
      // SSR: use injected service
      return await globalThis.myService.getData();
    } else {
      // Client: use fetch API
      const response = await fetch("/api/data");
      return await response.json();
    }
  },
});
```

### 2. **Type Marshalling**

Not all types can be marshalled, for detailed information read the node-api-dotnet docs. [**JS/.NET Type mappings**](https://microsoft.github.io/node-api-dotnet/reference/js-dotnet-types.html)

### 3. **Performance Overhead**

Each service call involves marshalling between .NET and JavaScript. For extremely high-frequency calls, consider batching or caching, and keeping the services small.

## Additional configuration options

### Custom Type Destination

Override where generated types are copied:

```xml
<!-- In your .csproj -->
<PropertyGroup>
  <SsrTypesDestination>$(MSBuildProjectDirectory)/ClientApp/types/api.d.ts</SsrTypesDestination>
</PropertyGroup>
```
