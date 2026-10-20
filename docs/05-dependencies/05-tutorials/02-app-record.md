---
title: "Tutorial: App Record"
description: Move from explicit dependency parameters to a reusable environment record.
---

# Tutorial: App Record

This tutorial starts where [Explicit Dependencies First](explicit-dependencies.html) leaves off.

Explicit parameters become repetitive when:

- every helper has to thread the same dependencies
- adding one more dependency means touching many signatures
- the execution boundary grows as the feature adds dependencies

An app record bundles dependencies at the boundary while workflows name the fields they read.

## 1. Same Interfaces, Plus An Audit Log

This page redefines the interfaces and error union from the previous tutorial so it stands alone, then adds a third
dependency — `IAuditLog` — and the `AuditWriteFailed` error case. The two existing interfaces keep their signatures.

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
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
        let! orders = Flow.envWith _.Orders
        do!
            Flow.fromTaskResult(fun _ -> orders.Save order)
            |> Flow.mapError OrderRejected

        return order
    }

let sendConfirmation (order: Order) : Flow<AppEnv, PlaceOrderError, unit> =
    flow {
        let! email = Flow.envWith _.Email
        do!
            ColdTask(fun _ ->
                task {
                    do! email.SendConfirmation order
                    return ()
                })
    }

let writeAudit (message: string) : Flow<AppEnv, PlaceOrderError, unit> =
    flow {
        let! audit = Flow.envWith _.Audit
        return!
            Flow.fromTaskResult(fun _ -> audit.Write message)
            |> Flow.mapError (fun () -> AuditWriteFailed)
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

With an app record:

- helper functions stop carrying dependency parameters
- helper functions still say exactly which fields they read
- adding another dependency does not force you to redesign the whole feature

## 4. Real Implementations

```fsharp
type SqlOrderRepository() =
    interface IOrderRepository with
        member _.Save order =
            task {
                // In an application, this could use a database transaction or ORM.
                return Ok ()
            }

type SmtpEmailSender() =
    interface IEmailSender with
        member _.SendConfirmation order =
            task {
                // In an application, this could use SMTP or an email API client.
            }

type FileAuditLog() =
    interface IAuditLog with
        member _.Write message =
            task {
                // In an application, this could append to a file or use a structured logger or queue.
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

Production builds `AppEnv` from `SqlOrderRepository()`, `SmtpEmailSender()`, and `FileAuditLog()`. A test builds the
same record from the recording doubles:

```fsharp run
let saved = ResizeArray<Order>()
let sent = ResizeArray<string>()
let entries = ResizeArray<string>()

let env =
    { Orders = RecordingOrders(saved)
      Email = RecordingEmails(sent)
      Audit = RecordingAudit(entries) }

let order =
    { Id = OrderId(Guid.Parse "7d3c2e1a-0000-4000-8000-000000000002")
      Email = "ada@example.com"
      Total = 125m }

placeOrder order |> Flow.run env |> shouldEqual (Exit.Success order.Id)
List.ofSeq saved |> shouldEqual [ order ]
List.ofSeq sent |> shouldEqual [ "ada@example.com" ]
List.ofSeq entries |> shouldEqual [ "Placed ada@example.com for 125" ]
```

A failing save reaches the repository with the same order, then short-circuits the email and audit steps:

```fsharp run
let rejectedSaved = ResizeArray<Order>()
let rejectedSent = ResizeArray<string>()
let rejectedEntries = ResizeArray<string>()

let rejecting =
    { new IOrderRepository with
        member _.Save order =
            task {
                rejectedSaved.Add order
                return Error "declined"
            } }

let rejectedEnv =
    { Orders = rejecting
      Email = RecordingEmails(rejectedSent)
      Audit = RecordingAudit(rejectedEntries) }

let rejectedOrder =
    { Id = OrderId(Guid.Parse "7d3c2e1a-0000-4000-8000-000000000002")
      Email = "ada@example.com"
      Total = 125m }

placeOrder rejectedOrder
|> Flow.run rejectedEnv
|> shouldEqual (Exit.Failure(Cause.Fail(OrderRejected "declined")))

List.ofSeq rejectedSaved |> shouldEqual [ rejectedOrder ]
List.ofSeq rejectedSent |> shouldEqual []
List.ofSeq rejectedEntries |> shouldEqual []
```

`Flow.run env` is the only place a record *value* is supplied. Helpers name the record *type* in their
`Flow<AppEnv, …>` annotations and read individual fields with `Flow.envWith`, so they never receive the whole
record.

## When To Stop Here

Keep the app record if it meets the application's needs.

Move beyond it when:

- you want reusable helpers to depend on named contracts instead of record field names
- you want startup-time provisioning with failure handling
- you want scope-owned resources and cleanup

Continue with [Tutorial: Composing Built-in Services](/services/existing-services.html) to add `BaseRuntime`
alongside dependencies like this one, [Tutorial: Creating Reusable Services](/advanced/custom-services.html) to
name a contract like `IOrderRepository` the way the built-in services are named, and then
[Tutorial: Layers](/layers/tutorial.html).
