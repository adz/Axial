---
title: "Tutorial: Explicit Dependencies First"
description: Pass plain function arguments and interfaces first, and introduce an environment when they stop scaling.
---

# Tutorial: Explicit Dependencies First

This tutorial starts one step before `Flow<'env, 'error, 'value>`. Define small interfaces, pass them explicitly, and compose a few operations before introducing an environment record.

Use this approach first when:

- the workflow is still local to one feature
- you want to prove the dependency boundaries before choosing an environment shape
- you want direct tests without building an environment first

## 1. Define The Contract

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

```fsharp
open System
open System.Threading.Tasks

type OrderId = OrderId of Guid

type Order =
    { Id: OrderId
      Email: string
      Total: decimal }

type PlaceOrderError =
    | InvalidEmail
    | OrderRejected of string
    | TimedOut
    | Cancelled

type IOrderRepository =
    abstract Save : Order -> Task<Result<unit, string>>

type IEmailSender =
    abstract SendConfirmation : Order -> Task
```

These interfaces are intentionally narrow. They represent what the workflow needs, not the full database or mail client API.

## 2. Compose Small Flows

```fsharp
let validateOrder (order: Order) : Result<Order, PlaceOrderError> =
    if String.IsNullOrWhiteSpace order.Email then
        Error InvalidEmail
    else
        Ok order

let saveOrder (orders: IOrderRepository) (order: Order) : Flow<unit, PlaceOrderError, Order> =
    flow {
        do!
            Flow.fromTaskResult(fun _ -> orders.Save order)
            |> Flow.mapError OrderRejected

        return order
    }

let sendConfirmation (email: IEmailSender) (order: Order) : Flow<unit, PlaceOrderError, unit> =
    flow {
        do!
            ColdTask(fun _ ->
                task {
                    do! email.SendConfirmation order
                    return ()
                })
    }

let placeOrder
    (orders: IOrderRepository)
    (email: IEmailSender)
    (order: Order)
    : Flow<unit, PlaceOrderError, OrderId> =
    flow {
        let! validOrder = validateOrder order
        let! savedOrder = saveOrder orders validOrder
        do! sendConfirmation email savedOrder
        return savedOrder.Id
    }
```

Nothing is hidden here:

- pure validation stays in `Result`
- each dependency is passed explicitly
- `Flow` is only used where async work and typed execution outcomes matter

## 3. Realistic Implementations

```fsharp
type SqlOrderRepository() =
    interface IOrderRepository with
        member _.Save order =
            task {
                // Imagine the real dependency here: DbConnection, EF Core, Dapper, etc.
                printfn "Saving %A to the database" order.Id
                return Ok ()
            }

type SmtpEmailSender() =
    interface IEmailSender with
        member _.SendConfirmation order =
            task {
                // Imagine the real dependency here: SMTP client, SendGrid SDK, etc.
                printfn "Sending order email to %s" order.Email
            }
```

## 4. Test Implementations

```fsharp
type RecordingOrderRepository(saved: ResizeArray<Order>) =
    interface IOrderRepository with
        member _.Save order =
            task {
                saved.Add order
                return Ok ()
            }

type RecordingEmailSender(sent: ResizeArray<string>) =
    interface IEmailSender with
        member _.SendConfirmation order =
            task {
                sent.Add order.Email
            }
```

These test doubles are boring on purpose. If this shape is awkward to test, the production dependency boundary is not sharp enough yet.

## 5. Run The Flow

In production, `placeOrder` gets `SqlOrderRepository()` and `SmtpEmailSender()`. A test gives it the recording
doubles and checks both the outcome and what each dependency saw:

```fsharp run
let saved = ResizeArray<Order>()
let sent = ResizeArray<string>()
let orders = RecordingOrderRepository(saved)
let email = RecordingEmailSender(sent)

let order =
    { Id = OrderId(Guid.Parse "7d3c2e1a-0000-4000-8000-000000000001")
      Email = "ada@example.com"
      Total = 99.95m }

placeOrder orders email order |> Flow.run () |> shouldEqual (Exit.Success order.Id)
List.ofSeq sent |> shouldEqual [ "ada@example.com" ]

placeOrder orders email { order with Email = " " } |> Flow.run () |> shouldEqual (Exit.Failure(Cause.Fail InvalidEmail))
saved.Count |> shouldEqual 1
```

The invalid order failed validation before either dependency was called: one order was saved and one email sent.

At the application edge, turn the `Exit` into whatever the caller needs:

```fsharp
let describe (exit: Exit<OrderId, PlaceOrderError>) =
    match exit with
    | Exit.Success _ -> "placed"
    | Exit.Failure(Cause.Fail InvalidEmail) -> "rejected before any dependency was called"
    | Exit.Failure(Cause.Fail(OrderRejected reason)) -> $"the repository rejected the order: {reason}"
    | Exit.Failure cause when Cause.isInterrupted cause -> "interrupted"
    | Exit.Failure _ -> "unexpected failure"
```

```fsharp run
Exit.Failure(Cause.Fail(OrderRejected "duplicate")) |> describe |> shouldEqual "the repository rejected the order: duplicate"
```

## 6. Why This Stops Scaling

Passing two dependencies explicitly is fine. Passing five through every helper is not.

That is the point where you move to an environment record:

- the workflow code still depends on the same interfaces
- the execution boundary gets cleaner
- adding a third dependency becomes additive instead of rewriting every call site

Continue with [Tutorial: App Record](app-record.html).
