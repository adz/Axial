---
weight: 40
title: "Tutorial: AppHost"
description: Integration with .NET Generic Host and Dependency Injection.
type: docs
---


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

The `FsFlow.Hosting` package provides a `Hosting.run` helper that automatically creates a `DefaultHost` (containing logging and clock) from the service provider and combines it with your custom app environment.

```fsharp
open FsFlow
open FsFlow.Hosting

type AppEnv = { TraceId: string }

let placeOrderWithLog order =
    flow {
        // We now have access to the host slice and the app environment as one context
        do! Logger.logWith (fun ctx -> sprintf "[%s] Placing order %A" ctx.AppEnv.TraceId order.Id)

        let! orders = Resolver.fromProvider<IOrderRepository>
        do! orders.Save order

        return order.Id
    }

let sp = host.Services
let env = { TraceId = "abc-123" }

// Hosting.run bridges the provider into the flow
let run () = task {
    let! result = Hosting.run sp env (placeOrderWithLog order)
    printfn "Result: %A" result
}

run().GetAwaiter().GetResult()
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
        task {
            let! exit = Flow.run env (placeOrder order)
            return exit
        }
```

## Why use AppHost integration?

- **Interoperability**: Works seamlessly with existing .NET libraries and middleware that rely on `IServiceProvider`.
- **Infrastructure Support**: Automatically inherits logging, configuration, and lifetime management from the .NET host.
- **Gradual Migration**: You can start using FsFlow in a single endpoint or background job within a large legacy application.

## Summary

You've now seen the full progression of dependency management in FsFlow:
1. **[AppRecord](./app-record/)**: Simple records for direct access.
2. **[HostContext](./host-context/)**: Splitting host services from app logic.
3. **[Capabilities](./capabilities/)**: Type-checked interface contracts.
4. **[AppHost](./app-host/)**: Integration with standard .NET containers.

Choose the pattern that matches the complexity and scale of your application.
