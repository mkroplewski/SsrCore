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
│ Pass Services  │───▶│ Node.js SSR Runtime │◀──│ Your React/Vue  │
│ as Parameter   │    │  (render function)   │   │ Components Use  │
│ to Render Fn   │    └─────────────────────┘   │ Services Param  │
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
3. These proxies are passed as the second parameter to your render function
4. Your JS code receives the services object and can call methods on it
5. Calls are marshalled back to .NET, executed, and results returned
6. After rendering completes, services are automatically cleaned up

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

**Step 4:** Use in your SSR code - services are passed as the second parameter to your render function:

```tsx
// server/render.tsx (or equivalent SSR entry point)
import type generated from "./_generated/api";

export async function render(
  request: Request,
  services: {
    greeterService: generated.greeterService;
  }
) {
  // Your rendering logic here - services available via the parameter
  const greeting = await services.greeterService.greetAsync("World");
  // ...
}
```

## Passing Services to Your Components

How you pass services from the render function to your components is **up to you**. You can use built-in framework options or create your own context pattern.

### React Router AppLoadContext

React Router has built-in support for `AppLoadContext`, which works perfectly with SsrCore services. The default `createRequestHandler` accepts services as the second parameter:

**Type declarations** (`app/global.d.ts`):

```typescript
import "react-router";
import { greeterService } from "./_generated/api";

declare module "react-router" {
  interface AppLoadContext {
    greeterService: greeterService;
  }
}
```

**That's it!** SsrCore automatically calls `createRequestHandler` with your services as the second parameter (AppLoadContext). You don't need to write a custom render function.

**Use in route loaders**:

```tsx
export const Route = createFileRoute("/")({
  loader: ({ context }) => {
    return {
      greeting: context.greeterService.greetAsync("World"),
    };
  },
  component: IndexComponent,
});
```

### TanStack Router Context

For TanStack Router, use the router context pattern with module augmentation:

**Services type** (`src/services.ts`):

```tsx
import services from "./_generated/api";

export type Services = {
  greeterService?: services.greeterService;
};
```

**Router context type** (`src/routerContext.tsx`):

```tsx
import { Services } from "./services";

export type RouterContext = {
  services: Services;
};
```

**Router setup** (`src/router.tsx`):

```tsx
import { createRouter as createReactRouter } from "@tanstack/react-router";
import { routeTree } from "./routeTree.gen";
import { Services } from "./services";

export function createRouter(services: Services) {
  return createReactRouter({
    routeTree,
    context: {
      head: "",
      services: services,
    },
    defaultPreload: "intent",
    scrollRestoration: true,
  });
}

declare module "@tanstack/react-router" {
  interface Register {
    router: ReturnType<typeof createRouter>;
  }
}
```

**Use in loaders**:

```tsx
export const Route = createFileRoute("/")({
  loader: ({ context }) => {
    if (context.services.greeterService) {
      return { greeting: context.services.greeterService.greetAsync("World") };
    }
    return { greeting: "Client" };
  },
});
```

### Option 3: Custom React Context

For maximum control or other frameworks, create your own context:

```tsx
import { createContext, useContext } from "react";
import type generated from "./_generated/api";

const ServicesContext = createContext<{
  greeterService?: generated.greeterService;
}>({});

export function ServicesProvider({ services, children }) {
  return <ServicesContext.Provider value={services}>{children}</ServicesContext.Provider>;
}

export function useServices() {
  return useContext(ServicesContext);
}

// In your component:
function MyComponent() {
  const { greeterService } = useServices();
  // use greeterService...
}
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

Services are **only available during SSR** (passed as parameter to render function), not on the client.

**Solution:** Implement separate client-side data fetching:

```typescript
export const Route = createFileRoute("/data")({
  loader: async ({ context }) => {
    if (context.services) {
      // SSR: use service from context
      return await context.services.myService.getData();
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
