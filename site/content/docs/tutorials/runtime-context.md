---
weight: 20
title: "Tutorial: HostContext"
description: Splitting host services from application dependencies.
type: docs
---


`HostContext<'host, 'appEnv>` allows you to separate operational concerns (the "how" of running your app) from business logic dependencies (the "what" your app does).

In this tutorial, we will split our previous order placement workflow into a "Host" slice and an "AppEnv" slice.

## 1. Define the Split

Define two records: one for operational services (like logging) and one for application services.

```fsharp
open System
open FsFlow

// 1. Host services (Operational concerns)
type AppHost =
    { Log: string -> unit }

// 2. Application services (Business logic)
type AppEnv =
    { Orders: IOrderRepository }
```

## 2. Write the Workflow using the Split

Use `Flow.readHost` and `Flow.readAppEnv` to access services from their respective slices.

```fsharp
let placeOrder order =
    flow {
        // Access host services
        let! host = Flow.readHost id
        host.Log (sprintf "Placing order %A" order.Id)

        // Access application services
        let! appEnv = Flow.readAppEnv id
        do! appEnv.Orders.Save order

        return order.Id
    }
```

## 3. Create the Context and Run

Construct both records and combine them into a `HostContext`.

```fsharp
open System.Threading

[<EntryPoint>]
let main _ =
    // Create the two slices
    let host = { Log = printfn "[LOG] %s" }
    let appEnv = { Orders = InMemoryOrders() }

    // Combine them into a HostContext
    let context = HostContext.create host appEnv CancellationToken.None

    let order = { Id = Guid.NewGuid(); Total = 99.99m }
    
    // Run the flow against the HostContext
    let run () = task {
        let! result = Flow.run context (placeOrder order)
        
        match result with
        | Exit.Success id -> return 0
        | Exit.Failure _ -> return 1
    }

    run().GetAwaiter().GetResult()
```

## Why use HostContext?

- **Logical Separation**: Keeps your business logic focused on the domain, while standardizing how operational services are handled across the whole application.
- **Better Observability**: You can easily wrap the `Host` with middleware for telemetry, tracing, and structured logging without changing your business code.
- **Environment Agnostic**: The `AppEnv` slice can change based on the specific module or feature, while the `Host` stays consistent across the entire application host.

## Next Steps

When your application becomes large enough that the `AppEnv` record itself becomes too wide, you can use **[Capabilities](./capabilities/)** to define even smaller, nominal interface contracts.
