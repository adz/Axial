---
weight: 5
title: "Tutorial: Explicit Dependencies First"
description: Start with plain function arguments and interfaces before introducing an environment.
---

# Tutorial: Explicit Dependencies First

This tutorial starts one step before `Flow<'env, 'error, 'value>`. The point is to keep the domain shape obvious: define small interfaces, pass them explicitly, and compose a few operations before introducing an environment record.

Use this approach first when:

- the workflow is still local to one feature
- you want to prove the dependency boundaries before choosing an environment shape
- you want the easiest possible tests

## 1. Define The Contract

```fsharp
open System
open System.Threading.Tasks
open FsFlow

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
        let! saveResult = orders.Save order

        match saveResult with
        | Ok () -> return order
        | Error reason -> return! Flow.fail (OrderRejected reason)
    }

let sendConfirmation (email: IEmailSender) (order: Order) : Flow<unit, PlaceOrderError, unit> =
    flow {
        do! email.SendConfirmation order
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

```fsharp
let runExample () = task {
    let orders = SqlOrderRepository() :> IOrderRepository
    let email = SmtpEmailSender() :> IEmailSender

    let order =
        { Id = OrderId(Guid.NewGuid())
          Email = "ada@example.com"
          Total = 99.95m }

    let! exit = (placeOrder orders email order).ToTask(())

    match exit with
    | Exit.Success orderId ->
        printfn "Placed %A" orderId
    | Exit.Failure (Cause.Fail InvalidEmail) ->
        printfn "The order was rejected before any dependency was called."
    | Exit.Failure (Cause.Fail (OrderRejected reason)) ->
        printfn "The repository rejected the order: %s" reason
    | Exit.Failure Cause.Interrupt ->
        printfn "The workflow was interrupted."
    | Exit.Failure cause ->
        printfn "Unexpected failure: %s" (Cause.prettyPrint (function OrderRejected r -> r | _ -> "domain error") cause)
}
```

## 6. Why This Stops Scaling

Passing two dependencies explicitly is fine. Passing five through every helper is not.

That is the point where you move to an environment record:

- the workflow code still depends on the same interfaces
- the execution boundary gets cleaner
- adding a third dependency becomes additive instead of rewriting every call site

Continue with [Tutorial: AppRecord](./app-record/).
