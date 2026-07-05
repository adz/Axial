---
weight: 10
title: "Tutorial: App Record"
description: Move from explicit dependency parameters to a reusable environment record.
aliases:
  - /docs/tutorials/app-record/
type: docs
---


This tutorial starts where [Explicit Dependencies First](../explicit-dependencies/) leaves off.

The problem is not that explicit parameters are wrong. The problem is repetition:

- every helper has to thread the same dependencies
- adding one more dependency means touching many signatures
- the execution boundary grows as the feature adds dependencies

An app record solves that by bundling dependencies once at the boundary while keeping the workflow code explicit.

## 1. Start With The Same Interfaces

```fsharp
open System
open System.Threading.Tasks
open Axial

type OrderId = OrderId of Guid

type Order =
    { Id: OrderId
      Email: string
      Total: decimal }

type PlaceOrderError =
    | InvalidEmail
    | OrderRejected of string
    | AuditWriteFailed

type IOrderRepository =
    abstract Save : Order -> Task<Result<unit, string>>

type IEmailSender =
    abstract SendConfirmation : Order -> Task

type IAuditLog =
    abstract Write : string -> Task<Result<unit, unit>>
```

## 2. Bundle Them Once

```fsharp
type AppEnv =
    { Orders: IOrderRepository
      Email: IEmailSender
      Audit: IAuditLog }
```

Adding a third dependency is now an additive change to the environment record rather than a rewrite of every helper signature.

## 3. Compose Several Flows

```fsharp
let validateOrder (order: Order) : Result<Order, PlaceOrderError> =
    if String.IsNullOrWhiteSpace order.Email then
        Error InvalidEmail
    else
        Ok order

let saveOrder (order: Order) : Flow<AppEnv, PlaceOrderError, Order> =
    flow {
        let! orders = Flow.read _.Orders
        let! saveResult = orders.Save order

        match saveResult with
        | Ok () -> return order
        | Error reason -> return! Flow.fail (OrderRejected reason)
    }

let sendConfirmation (order: Order) : Flow<AppEnv, PlaceOrderError, unit> =
    flow {
        let! email = Flow.read _.Email
        do! email.SendConfirmation order
    }

let writeAudit (message: string) : Flow<AppEnv, PlaceOrderError, unit> =
    flow {
        let! audit = Flow.read _.Audit
        let! result = audit.Write message

        match result with
        | Ok () -> return ()
        | Error () -> return! Flow.fail AuditWriteFailed
    }

let placeOrder (order: Order) : Flow<AppEnv, PlaceOrderError, OrderId> =
    flow {
        let! validOrder = validateOrder order
        let! savedOrder = saveOrder validOrder
        do! sendConfirmation savedOrder
        do! writeAudit $"Placed {savedOrder.Email} for {savedOrder.Total}"
        return savedOrder.Id
    }
```

This is the main win of an app record:

- helper functions stop carrying dependency parameters
- helper functions still say exactly which fields they read
- adding another dependency does not force you to redesign the whole feature

## 4. Real Implementations

```fsharp
type SqlOrderRepository() =
    interface IOrderRepository with
        member _.Save order =
            task {
                // Imagine the real dependency here: database transaction, ORM, etc.
                return Ok ()
            }

type SmtpEmailSender() =
    interface IEmailSender with
        member _.SendConfirmation order =
            task {
                // Imagine the real dependency here: SMTP or email API client.
            }

type FileAuditLog() =
    interface IAuditLog with
        member _.Write message =
            task {
                // Imagine the real dependency here: file append, structured logger, queue, etc.
                return Ok ()
            }
```

## 5. Test Implementations

```fsharp
type RecordingOrders(saved: ResizeArray<Order>) =
    interface IOrderRepository with
        member _.Save order =
            task {
                saved.Add order
                return Ok ()
            }

type RecordingEmails(sent: ResizeArray<string>) =
    interface IEmailSender with
        member _.SendConfirmation order =
            task {
                sent.Add order.Email
            }

type RecordingAudit(entries: ResizeArray<string>) =
    interface IAuditLog with
        member _.Write message =
            task {
                entries.Add message
                return Ok ()
            }
```

## 6. Run The Workflow

```fsharp
let run () = task {
    let env =
        { Orders = SqlOrderRepository() :> IOrderRepository
          Email = SmtpEmailSender() :> IEmailSender
          Audit = FileAuditLog() :> IAuditLog }

    let order =
        { Id = OrderId(Guid.NewGuid())
          Email = "ada@example.com"
          Total = 125m }

    let! exit = (placeOrder order).ToTask(env)

    match exit with
    | Exit.Success orderId ->
        printfn "Placed %A" orderId
    | Exit.Failure cause ->
        printfn "%s" (Cause.prettyPrint (function
            | InvalidEmail -> "invalid email"
            | OrderRejected reason -> reason
            | AuditWriteFailed -> "audit write failed") cause)
}
```

## When To Stop Here

An app record is enough for a lot of applications.

Move beyond it when:

- you want reusable helpers to depend on named contracts instead of record field names
- you want startup-time provisioning with failure handling
- you want scope-owned resources and cleanup

Continue with [Tutorial: Creating Reusable Services](../custom-services/) and then [Tutorial: Layers](../layers/).
