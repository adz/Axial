---
weight: 10
title: Explicit Services
description: Choosing between records, IHas service contracts, and Service accessors.
type: docs
---


FsFlow workflows declare what they need through `Flow<'env, 'error, 'value>`. The environment is an ordinary F# value.
That value can be a small record, a larger application record, or an object that implements named service contracts.

## Start With Records

For feature-local code, prefer records and `Flow.read`.

```fsharp
type CheckoutEnv =
    { Orders: IOrderRepository
      Email: IEmailSender }

let submit order : Flow<CheckoutEnv, CheckoutError, unit> =
    flow {
        let! orders = Flow.read _.Orders
        let! email = Flow.read _.Email

        do! orders.Save order
        do! email.SendConfirmation order
    }
```

This is the default because the requirement is visible and the test setup is just another record.

## Use IHas For Reusable Services

Use `IHas<'service>` when a helper module should advertise one named dependency without caring about the concrete
environment record.

```fsharp
type IHasOrders =
    inherit IHas<IOrderRepository>

let save order : Flow<#IHasOrders, CheckoutError, unit> =
    flow {
        let! orders = Service<IOrderRepository>.get()
        do! orders.Save order
    }
```

Application environments can implement many `IHas<'service>` contracts:

```fsharp
type AppEnv =
    { Orders: IOrderRepository
      Email: IEmailSender }

    interface IHas<IOrderRepository> with
        member this.Service = this.Orders

    interface IHas<IEmailSender> with
        member this.Service = this.Email
```

`Service<'service>.get()` is statically checked. If the environment does not implement `IHas<'service>`, the workflow
does not type-check.

Layers do not automatically compose `IHas<'service>` implementations for you. Build a named environment record and
implement the contracts explicitly:

```fsharp
let appLayer =
    Layer.merge ordersLayer emailLayer
    |> Layer.map (fun (orders, email) ->
        { Orders = orders
          Email = email })
```

This is more explicit than a generated or proxy environment, and it keeps compile errors tied to named application
types. FsFlow v1 does not include tagged services; when you need two values with the same service type, use named record
fields or distinct service contracts.

## Keep Resolve At The Edge

`Service<'service>.resolve()` reads from `IServiceProvider`. Use it in host glue or adapters where dynamic container
lookup is the intended behavior.

```fsharp
let loadFromHost : Flow<IServiceProvider, unit, IOrderRepository> =
    Service<IOrderRepository>.resolve()
```

Missing provider registrations are defects. If missing registrations should be typed startup errors, build an explicit
environment with a layer instead.
