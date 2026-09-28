---
title: Service Contracts
description: How a package asks for a dependency without knowing your environment type.
---

# Service Contracts

A record works because *you* own both sides: the workflow names `AppEnv`, and you supply an `AppEnv`. A package
author cannot do that. `Axial.Console` is compiled long before your `AppEnv` exists, so `Console.writeLine` cannot
mention it.

A contract is how the package asks anyway. It is an ordinary interface named `IHasFoo`, with a single
member `Foo`:

The examples on this page use these application types:

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Layers
open Axial.Console
open Axial.FileSystem
open Axial.Hosting
open Axial.Hosting.Browser
open Axial.Hosting.Node
open Axial.PlatformService
open Axial.State
open Axial.Telemetry
open Axial.Telemetry.JavaScript

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp prepare
type Order = { Id: int }
type CheckoutError = AlreadySaved of int

type IOrderRepository =
    abstract Save: Order -> Result<unit, CheckoutError>

type IEmailSender =
    abstract SendConfirmation: Order -> unit
```

```fsharp
type IHasOrders =
    abstract Orders: IOrderRepository
```

The helper constrains its environment to that interface instead of naming a type:

```fsharp
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
[`Flow.envWith`](the-environment.html); the interface only says which member it may read.

## Supplying one

Your record implements the contracts it satisfies. One line each, and the fields keep whatever names you gave them:

```fsharp
type IHasEmail =
    abstract Email: IEmailSender

[<RequireQualifiedAccess>]
module Email =
    let service<'env, 'error when 'env :> IHasEmail> : Flow<'env, 'error, IEmailSender> =
        Flow.envWith _.Email

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

```fsharp
let submit<'env when 'env :> IHasOrders and 'env :> IHasEmail> (order: Order) : Flow<'env, CheckoutError, unit> =
    flow {
        let! orders = Orders.service
        let! email = Email.service
        do! orders.Save order
        email.SendConfirmation order
    }
```

`AppEnv` satisfies both constraints, so it runs `save` and `submit` alike:

```fsharp run
let saved = ResizeArray<int>()
let sent = ResizeArray<int>()

let env =
    { Orders =
        { new IOrderRepository with
            member _.Save order =
                if saved.Contains order.Id then
                    Error(AlreadySaved order.Id)
                else
                    saved.Add order.Id
                    Ok() }
      Email = { new IEmailSender with member _.SendConfirmation order = sent.Add order.Id } }

submit { Id = 1 } |> Flow.run env |> shouldEqual (Exit.Success())
save { Id = 1 } |> Flow.run env |> shouldEqual (Exit.Failure(Cause.Fail(AlreadySaved 1)))
List.ofSeq sent |> shouldEqual [ 1 ]
```

When a combination recurs, name it and a single constraint covers it:

```fsharp
type ICheckoutEnv =
    inherit IHasOrders
    inherit IHasEmail

let submitOrder (order: Order) : Flow<#ICheckoutEnv, CheckoutError, unit> = submit order
```

`#IHasOrders` carries exactly one constraint, which is why the aggregate exists. Use the explicit
`<'env when 'env :> … and 'env :> …>` form when you need several, or when the environment appears more than once in a
signature, because each occurrence of `#T` is a separate type variable.

## When to declare one

For application code, don't. A record is simpler, needs no interface, and is what
[the environment](the-environment.html) documents.

Declare a contract when you are **publishing a helper whose callers you will never see**, such as a shared library or a
package like `Axial.FileSystem`. A record cannot cover that case, because the helper cannot know the caller's record
type.

Writing a package that ships services is covered in
[providing services from a package](/advanced/reusable-packages.html).
