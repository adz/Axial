---
title: "Tutorial: Runtime Operations"
description: Timeout, retry, cancellation, and runtime annotations.
---

# Tutorial: Runtime Operations

This tutorial focuses on the operational helpers that sit around a workflow: timeout, retry, cancellation, annotations, and exception translation.

Use these helpers at the application boundary. They are not substitutes for domain rules.

## A Small Workflow To Wrap

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
open System.Threading

type CheckoutError =
    | GatewayUnavailable
    | CheckoutTimedOut
    | ReceiptStoreFailed
    | UnexpectedGatewayFailure of string

let authorizeCard : Flow<unit, CheckoutError, string> =
    flow {
        do! Flow.sleep (TimeSpan.FromMilliseconds 50.0)
        return "receipt-123"
    }

let storeReceipt (receiptId: string) : Flow<unit, CheckoutError, unit> =
    flow {
        do! Flow.sleep (TimeSpan.FromMilliseconds 20.0)
        return ()
    }

let notifyCustomer (receiptId: string) : Flow<unit, CheckoutError, unit> =
    flow {
        do! Flow.sleep (TimeSpan.FromMilliseconds 20.0)
        return ()
    }

let checkout : Flow<unit, CheckoutError, string> =
    flow {
        let! receiptId = authorizeCard
        do! storeReceipt receiptId
        do! notifyCustomer receiptId
        return receiptId
    }
```

Even in this tiny example there are already several composed steps. Runtime helpers answer how this execution should behave when one of those steps is slow, flaky, canceled, or throws.

## Timeout

```fsharp
let checkoutWithTimeout =
    checkout
    |> Flow.timeoutToError (TimeSpan.FromMilliseconds 10.0) CheckoutTimedOut
```

Authorizing the card takes 50 ms, so the 10 ms limit fails the checkout:

```fsharp run
checkoutWithTimeout |> Flow.run () |> shouldEqual (Exit.Failure(Cause.Fail CheckoutTimedOut))
```

`timeout`, `timeoutToError`, `timeoutToOk`, and `timeoutWith` are boundary tools. They answer "what should this workflow do if it takes too long?"

## Retry

```fsharp
let retryingCheckout =
    checkout
    |> Flow.retry (
        Schedule.exponential (TimeSpan.FromMilliseconds 50.0)
        |> Schedule.recursAtMost 3
        |> Schedule.whileInput (function
            | GatewayUnavailable
            | ReceiptStoreFailed -> true
            | _ -> false))
```

```fsharp run
retryingCheckout |> Flow.run () |> shouldEqual (Exit.Success "receipt-123")
```

`Flow.retry` takes a `Schedule`, which sees each typed error: `Schedule.whileInput` selects the errors worth retrying,
and `Schedule.recursAtMost` bounds the attempts. For the common case, `Retry.schedule` builds the same schedule from a record
with named fields.

## Exceptions

```fsharp
let rawGatewayCall (clientExplodes: bool) : Flow<unit, CheckoutError, string> =
    flow {
        if clientExplodes then
            return raise (InvalidOperationException "gateway client exploded")

        return "receipt-123"
    }

let safeGatewayCall =
    rawGatewayCall false
    |> Flow.catch (fun ex -> UnexpectedGatewayFailure ex.Message)
```

```fsharp run
rawGatewayCall true
|> Flow.catch (fun ex -> UnexpectedGatewayFailure ex.Message)
|> Flow.run ()
|> shouldEqual (Exit.Failure(Cause.Fail(UnexpectedGatewayFailure "gateway client exploded")))
```

Use `Flow.catch` when you are deliberately translating technical exceptions into your typed error channel. If you do not catch them, they surface as `Cause.Die` in the final `Exit`.

## Cancellation

```fsharp
let runCancellable (cancellationToken: CancellationToken) =
    task {
        let! exit = checkout.StartAsTask((), cancellationToken = cancellationToken)

        match exit with
        | Exit.Success receipt -> return $"Receipt {receipt}"
        | Exit.Failure Cause.Interrupt -> return "Cancelled"
        | Exit.Failure cause -> return Cause.prettyPrint string cause
    }
```

```fsharp run
runCancellable CancellationToken.None |> _.Result |> shouldEqual "Receipt receipt-123"
runCancellable (new CancellationToken(true)) |> _.Result |> shouldEqual "Cancelled"
```

If the host cancels the token, the flow finishes with `Exit.Failure Cause.Interrupt`.

Cancellation stays an interruption inside the workflow. Whoever requested it decides what it means, as `runCancellable`
does by matching `Cause.Interrupt`. That keeps a `Canceled` case out of every error type, and keeps retries and error
handlers from treating a cancellation as a failure.

Two helpers cover the edges:

- `Flow.ensureNotCanceled` stops with `Cause.Interrupt` if the token has been cancelled. Put it at safe points in long
  work that does not otherwise observe cancellation, such as a CPU-bound loop.
- `Flow.catchCancellation` turns cancellation that a library raised for its own reasons, while the token was still
  live, into a typed error. Without it, that cancellation is a defect: nobody asked for it.

## Annotations

```fsharp
let annotatedCharge : Flow<unit, CheckoutError, Map<string, string> * string option> =
    flow {
        let! annotations = Flow.annotations
        let! traceId = Flow.traceId
        return annotations, traceId
    }
```

```fsharp run
annotatedCharge
|> Flow.annotate "order_id" "o-7"
|> Flow.withTraceId "trace-1"
|> Flow.run ()
|> shouldEqual (Exit.Success(Map [ "order_id", "o-7"; "trace_id", "trace-1" ], Some "trace-1"))
```

Annotations are useful for observability and correlation. They belong to runtime mechanics, not to your domain model.

## Pulling It Together

```fsharp
let guardedCheckout =
    safeGatewayCall
    |> Flow.bind (fun receiptId ->
        flow {
            do! storeReceipt receiptId
            do! notifyCustomer receiptId
            return receiptId
        })
    |> Flow.timeoutToError (TimeSpan.FromSeconds 2.0) CheckoutTimedOut
    |> Flow.retry (
        Retry.schedule
            { Retry.defaults with
                When =
                    function
                    | GatewayUnavailable
                    | ReceiptStoreFailed -> true
                    | _ -> false })
```

```fsharp run
guardedCheckout |> Flow.run () |> shouldEqual (Exit.Success "receipt-123")
```

Each concern has its own place:

- domain validation decides whether the operation should happen at all
- runtime helpers decide how the host should run that operation
- `Flow.catch` decides which technical exceptions should become typed failures
