---
weight: 20
title: "Tutorial: RuntimeContext"
description: Splitting host runtime services from application dependencies.
---

# Tutorial: RuntimeContext

`RuntimeContext<'runtime, 'env>` allows you to separate operational concerns (the "how" of running your app) from business logic dependencies (the "what" your app does).

In this tutorial, we will split our previous order placement workflow into a "Runtime" slice and an "Environment" slice.

## 1. Define the Split

Define two records: one for operational services (like logging) and one for application services.

```fsharp
open System
open FsFlow

// 1. Runtime services (Operational concerns)
type AppRuntime =
    { Log: string -> unit }

// 2. Application services (Business logic)
type AppEnv =
    { Orders: IOrderRepository }
```

## 2. Write the Workflow using the Split

Use `Flow.readRuntime` and `Flow.readEnvironment` to access services from their respective slices.

```fsharp
let placeOrder order =
    flow {
        // Access runtime services
        let! runtime = Flow.readRuntime id
        runtime.Log (sprintf "Placing order %A" order.Id)

        // Access application services
        let! app = Flow.readEnvironment id
        do! app.Orders.Save order

        return order.Id
    }
```

## 3. Create the Context and Run

Construct both records and combine them into a `RuntimeContext`.

```fsharp
open System.Threading

[<EntryPoint>]
let main _ =
    // Create the two slices
    let runtime = { Log = printfn "[LOG] %s" }
    let app = { Orders = InMemoryOrders() }

    // Combine them into a RuntimeContext
    let context = RuntimeContext.create runtime app CancellationToken.None

    let order = { Id = Guid.NewGuid(); Total = 99.99m }
    
    // Run the flow against the RuntimeContext
    let result = Flow.run context (placeOrder order)
    
    match result.Wait() with
    | Exit.Success id -> 0
    | Exit.Failure _ -> 1
```

## Why use RuntimeContext?

- **Logical Separation**: Keeps your business logic focused on the domain, while standardizing how operational services are handled across the whole application.
- **Better Observability**: You can easily wrap the `Runtime` with middleware for telemetry, tracing, and structured logging without changing your business code.
- **Environment Agnostic**: The `Environment` slice can change based on the specific module or feature, while the `Runtime` stays consistent across the entire application host.

## Next Steps

When your application becomes large enough that the `Environment` record itself becomes too wide, you can use **[Capabilities](./capabilities/)** to define even smaller, nominal interface contracts.
