---
weight: 30
title: "Tutorial: Capabilities"
description: Using nominal interface contracts as the environment.
---

# Tutorial: Capabilities

Nominal Capability Contracts use F# interfaces to name the dependency surface. This allows the compiler to check that your environment implements all the required capabilities and makes it easy to reuse helpers across different workflows.

In this tutorial, we will refactor our workflow to use interface-based capabilities instead of concrete records.

## 1. Define Capability Interfaces

Instead of one big record, define small, focused interfaces.

```fsharp
open System
open System.Threading.Tasks
open FsFlow

type IHasOrders =
    abstract Orders: IOrderRepository

type IHasEmail =
    abstract Email: IEmailSender

// You can also group them into a larger contract
type IAppCaps =
    inherit IHasOrders
    inherit IHasEmail
```

## 2. Write Helpers using Capability Constraints

Helper functions can now specify exactly which capabilities they need using the `#` flexible type constraint.

```fsharp
let saveOrder order : Flow<#IHasOrders, _, _> =
    flow {
        let! repo = Flow.read _.Orders
        do! repo.Save order
    }

let sendEmail order : Flow<#IHasEmail, _, _> =
    flow {
        let! sender = Flow.read _.Email
        do! sender.SendConfirmation order
    }
```

## 3. Compose the Main Workflow

The main workflow combines these helpers. The compiler will infer that the environment must implement both `IHasOrders` and `IHasEmail`.

```fsharp
let placeOrder order =
    flow {
        do! saveOrder order
        do! sendEmail order
        return order.Id
    }
```

## 4. Implement the Environment

Your application environment is now just a class or record that implements the required interfaces.

```fsharp
type AppEnv =
    { Orders: IOrderRepository
      Email: IEmailSender }
    interface IHasOrders with member x.Orders = x.Orders
    interface IHasEmail with member x.Email = x.Email

[<EntryPoint>]
let main _ =
    let env = 
        { Orders = InMemoryOrders()
          Email = ConsoleEmail() }

    // env matches both #IHasOrders and #IHasEmail
    let result = Flow.run env (placeOrder order)
    // ...
```

## Why use Capabilities?

- **Refactor Safety**: If you add a new capability to a helper, the compiler will immediately tell you every call site that needs to be updated.
- **Granular Dependencies**: Helpers only ask for what they actually need, making the code easier to reason about and test.
- **Reusable Logic**: You can write general-purpose helpers (like "retry with logging") that work on any environment that provides the required capabilities.

## Next Steps

For enterprise applications that use standard .NET dependency injection, proceed to the **[AppHost](./app-host/)** tutorial to see how to bridge `IServiceProvider` into the FsFlow world.
