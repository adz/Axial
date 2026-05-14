---
weight: 40
title: "Tutorial: AppHost"
description: Integration with .NET Generic Host and Dependency Injection.
---

# Tutorial: AppHost

In enterprise .NET applications, you typically use a dependency injection (DI) container managed by `IHostBuilder` or `IWebHostBuilder`. FsFlow provides a bridge to adapt these containers into typed workflows.

In this tutorial, we will integrate FsFlow into a standard .NET worker service or web app.

## 1. Register Services in the Container

In your `Program.fs` or `Startup.fs`, register your services as you normally would.

```fsharp
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

let host =
    Host.CreateDefaultBuilder()
        .ConfigureServices(fun services ->
            services.AddSingleton<IOrderRepository, InMemoryOrders>()
            services.AddSingleton<IEmailSender, ConsoleEmail>()
        )
        .Build()
```

## 2. Bridge the Provider to a Workflow

You can use `Resolver.fromProvider` to read a service directly from `IServiceProvider`.

```fsharp
open FsFlow

let placeOrder order : Flow<IServiceProvider, MissingCapability, Guid> =
    flow {
        // Read services directly from the DI container
        let! orders = Resolver.fromProvider<IOrderRepository>
        let! email = Resolver.fromProvider<IEmailSender>

        do! orders.Save order
        do! email.SendConfirmation order

        return order.Id
    }
```

## 3. Use Hosting Helpers

The `FsFlow.Hosting` package provides a `Hosting.run` helper that automatically creates a `DefaultRuntime` (containing logging and clock) from the service provider and combines it with your custom environment.

```fsharp
open FsFlow.Hosting

type AppEnv = { TraceId: string }

let placeOrderWithLog order =
    flow {
        // We now have access to Runtime (from DI) and Env (our record)
        let! logger = Flow.readRuntime _.Logger
        let! env = Flow.readEnvironment id
        
        logger (sprintf "[%s] Placing order %A" env.TraceId order.Id)

        let! orders = Resolver.fromProvider<IOrderRepository>
        do! orders.Save order

        return order.Id
    }

let sp = host.Services
let env = { TraceId = "abc-123" }

// Hosting.run bridges the provider into the flow
let result = Hosting.run sp env (placeOrderWithLog order)
```

## 4. The Preferred Pattern: Adapting at the Boundary

While `Resolver.fromProvider` is useful for gradual migration, the preferred pattern is to adapt the DI container into a typed record or capability contract *once* at the edge (e.g., in a Controller or BackgroundService).

```fsharp
type OrderHandler(sp: IServiceProvider) =
    member _.Handle(order) =
        // Adapt DI once into a concrete AppEnv record
        let env = 
            { Orders = sp.GetRequiredService<IOrderRepository>()
              Email = sp.GetRequiredService<IEmailSender>() }
        
        // Now use the simpler AppRecord or Capabilities pattern
        Flow.run env (placeOrder order)
```

## Why use AppHost integration?

- **Interoperability**: Works seamlessly with existing .NET libraries and middleware that rely on `IServiceProvider`.
- **Infrastructure Support**: Automatically inherits logging, configuration, and lifetime management from the .NET host.
- **Gradual Migration**: You can start using FsFlow in a single endpoint or background job within a large legacy application.

## Summary

You've now seen the full progression of dependency management in FsFlow:
1. **[AppRecord](./app-record/)**: Simple records for direct access.
2. **[RuntimeContext](./runtime-context/)**: Splitting ops from domain logic.
3. **[Capabilities](./capabilities/)**: Type-checked interface contracts.
4. **[AppHost](./app-host/)**: Integration with standard .NET containers.

Choose the pattern that matches the complexity and scale of your application.
