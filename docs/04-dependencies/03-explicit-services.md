---
title: Explicit Services
description: Choosing between records, IHas service contracts, and Service accessors.
---

# Explicit Services

Axial workflows declare what they need through `Flow<'env, 'error, 'value>`. The environment is an ordinary F# value.
That value can be a small record, a larger application record, or an object that implements named service contracts.

## Plain Records For Feature-Local Code

For feature-local code, prefer records and `Flow.read`.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
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

## Declare A Contract For Reusable Services

Declare a contract when a helper should advertise one named dependency without caring about the concrete environment
record. A contract is an ordinary interface: one named `IHasFoo` exposing a single member `Foo`.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
type IHasOrders =
    abstract Orders : IOrderRepository

[<RequireQualifiedAccess>]
module Orders =
    let service<'env, 'error when 'env :> IHasOrders> : Flow<'env, 'error, IOrderRepository> =
        Flow.read _.Orders

let save order : Flow<#IHasOrders, CheckoutError, unit> =
    flow {
        let! orders = Orders.service
        do! orders.Save order
    }
```

The accessor is bound at module level on purpose. `Flow.read _.Orders` cannot resolve inside a `flow { }` block,
where the lambda's parameter type is not yet known; binding it next to its own type annotation resolves it once, and
callers then bind it with no annotation at all.

Application environments implement as many contracts as they supply:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
type AppEnv =
    { Orders: IOrderRepository
      Email: IEmailSender }

    interface IHasOrders with member this.Orders = this.Orders
    interface IHasEmail with member this.Email = this.Email
```

`member this.Orders = this.Orders` is not recursive. F# interface implementations are always explicit, so the
interface member is not in scope on the record; the right-hand side resolves to the field. An environment with no
such field fails to compile rather than recursing.

Because these are distinct interfaces, a workflow can require several at once and the constraints merge on their
own:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let checkout order : Flow<#IHasOrders, CheckoutError, unit> = ...

let submit<'env when 'env :> IHasOrders and 'env :> IHasEmail> order : Flow<'env, CheckoutError, unit> =
    flow {
        let! orders = Orders.service
        let! email = Email.service
        do! orders.Save order
        do! email.SendConfirmation order
    }
```

Name the combination when it recurs, and a single constraint covers it. Do not let a contract inherit a *generic*
interface: a type cannot be constrained by two instantiations of one generic interface, so a generic parent makes
the contract impossible to combine with any other.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
type ICheckoutEnv =
    inherit IHasOrders
    inherit IHasEmail

let submit order : Flow<#ICheckoutEnv, CheckoutError, unit> = ...
```

Layers do not compose contract implementations for you. Build a named environment record and implement the contracts
explicitly:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let appLayer =
    Layer.merge ordersLayer emailLayer
    |> Layer.map (fun (orders, email) ->
        { Orders = orders
          Email = email })
```

This is more explicit than a generated or proxy environment, and it keeps compile errors tied to named application
types. When you need two values of the same service type, use distinct contracts rather than one contract twice.

## Keep Resolve At The Edge

`ServiceProvider.get` reads from `IServiceProvider`. Use it in host glue or adapters where dynamic container
lookup is the intended behavior.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let loadFromHost : Flow<IServiceProvider, unit, IOrderRepository> =
    ServiceProvider.get<IOrderRepository, _, _>()
```

Missing provider registrations are defects. If missing registrations should be typed startup errors, build an explicit
environment with a layer instead.
