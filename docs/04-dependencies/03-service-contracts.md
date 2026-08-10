---
title: Service Contracts
description: How a package asks for a dependency without knowing your environment type.
---

# Service Contracts

A record works because *you* own both sides: the workflow names `AppEnv`, and you supply an `AppEnv`. A package
author cannot do that. `Axial.Console` is compiled long before your `AppEnv` exists, so `Console.writeLine` cannot
mention it.

A contract is how the package asks anyway. It is an ordinary interface — one named `IHasFoo`, exposing a single
member `Foo`:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
type IHasOrders =
    abstract Orders : IOrderRepository
```

The helper constrains its environment to that interface instead of naming a type:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
[<RequireQualifiedAccess>]
module Orders =
    let service<'env, 'error when 'env :> IHasOrders> : Flow<'env, 'error, IOrderRepository> =
        Flow.envWith _.Orders

let save order : Flow<#IHasOrders, CheckoutError, unit> =
    flow {
        let! orders = Orders.service
        do! orders.Save order
    }
```

Read `Flow<#IHasOrders, …>` as "any environment that can give me an `IOrderRepository`". This is still just
[`Flow.envWith`](the-environment.html) — the interface only says which member it may read.

## Supplying one

Your record implements the contracts it satisfies. One line each, and the fields keep whatever names you gave them:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
type AppEnv =
    { Orders: IOrderRepository
      Email: IEmailSender }

    interface IHasOrders with member this.Orders = this.Orders
    interface IHasEmail with member this.Email = this.Email
```

`member this.Orders = this.Orders` is not recursive. F# interface implementations are always explicit, so the
interface member is not in scope on the record itself and the right-hand side resolves to the field. A record with no
such field fails to compile rather than looping.

## Needing more than one

Contracts are distinct interfaces, so a workflow can require several and the constraints merge on their own:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let submit<'env when 'env :> IHasOrders and 'env :> IHasEmail> order : Flow<'env, CheckoutError, unit> =
    flow {
        let! orders = Orders.service
        let! email = Email.service
        do! orders.Save order
        do! email.SendConfirmation order
    }
```

When a combination recurs, name it and a single constraint covers it:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
type ICheckoutEnv =
    inherit IHasOrders
    inherit IHasEmail

let submit order : Flow<#ICheckoutEnv, CheckoutError, unit> = ...
```

`#IHasOrders` carries exactly one constraint, which is why the aggregate exists. Use the explicit
`<'env when 'env :> … and 'env :> …>` form when you need several, or when the environment appears more than once in a
signature — each occurrence of `#T` is a separate type variable.

## When to declare one

For application code, don't. A record is simpler, needs no interface, and is what
[the environment](the-environment.html) documents.

Declare a contract when you are **publishing a helper whose callers you will never see** — a shared library, or a
package like `Axial.FileSystem`. That is the case a record genuinely cannot cover, and it is the whole reason the
mechanism exists.

Writing a package that ships services is covered in
[providing services from a package](/advanced/reusable-packages.html).
