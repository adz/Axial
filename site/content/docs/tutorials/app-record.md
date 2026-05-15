---
weight: 10
title: "Tutorial: AppRecord"
description: Using a simple record as the application environment.
type: docs
---


The simplest way to manage dependencies in FsFlow is using a concrete record as your environment (`env`). This provides direct field access and minimal boilerplate.

In this tutorial, we will build a small order placement workflow that uses a record for its dependencies.

## 1. Define Your Dependencies

Start by defining the interfaces for your services and a record that bundles them together.

```fsharp
open System
open System.Threading.Tasks
open FsFlow

type Order = { Id: Guid; Total: decimal }

type IOrderRepository =
    abstract Save: Order -> Task<unit>

type IEmailSender =
    abstract SendConfirmation: Order -> Task<unit>

type AppEnv =
    { Orders: IOrderRepository
      Email: IEmailSender }
```

## 2. Write the Workflow

Use the `flow {}` builder to define your business logic. You can use `Flow.read` to project specific services from the environment.

```fsharp
let placeOrder order =
    flow {
        // Read specific services from the environment
        let! orders = Flow.read _.Orders
        let! email = Flow.read _.Email

        // Execute service methods (lifting Tasks into Flow)
        do! orders.Save order
        do! email.SendConfirmation order

        return order.Id
    }
```

## 3. Implement the Services

Create concrete implementations of your interfaces.

```fsharp
type InMemoryOrders() =
    interface IOrderRepository with
        member _.Save(order) = 
            printfn "Order %A saved" order.Id
            Task.CompletedTask

type ConsoleEmail() =
    interface IEmailSender with
        member _.SendConfirmation(order) = 
            printfn "Email sent for order %A" order.Id
            Task.CompletedTask
```

## 4. Run the App

Construct the `AppEnv` record and use `Flow.run` to execute your workflow.

```fsharp
[<EntryPoint>]
let main _ =
    let env = 
        { Orders = InMemoryOrders()
          Email = ConsoleEmail() }

    let order = { Id = Guid.NewGuid(); Total = 99.99m }
    
    // Run the flow and handle the result
    let run () = task {
        let! result = Flow.run env (placeOrder order)
        
        match result with
        | Exit.Success id -> 
            printfn "Successfully placed order: %A" id
        | Exit.Failure cause -> 
            printfn "Failed: %A" cause
    }

    run().GetAwaiter().GetResult()
    0
```

## Why use AppRecord?

- **Directness**: You can see exactly what fields are available in the environment.
- **Easy Testing**: You can easily swap out the entire record or individual fields for tests.
- **Low Ceremony**: No need for complex DI setup or interface hierarchies when starting out.

## Next Steps

As your application grows, you might find that you want to separate "host" services (like logging) from "business" services. Proceed to the **[HostContext](./host-context/)** tutorial to learn how to do that.
