---
title: "Tutorial: Runtime Operations"
description: Timeout, retry, cancellation, and runtime annotations.
---

# Tutorial: Runtime Operations

This tutorial focuses on the operational helpers that sit around a workflow: timeout, retry, cancellation, annotations, and exception translation.

Use these helpers at the application boundary. They are not substitutes for domain rules.

## A Small Workflow To Wrap

```fsharp
open System
open System.Threading

type CheckoutError =
    | GatewayUnavailable
    | CheckoutTimedOut
    | CheckoutCancelled
    | ReceiptStoreFailed
    | UnexpectedGatewayFailure of string

let authorizeCard : Flow<unit, CheckoutError, string> =
    flow {
        do! Flow.sleep (TimeSpan.FromMilliseconds 50)
        return "receipt-123"
    }

let storeReceipt (receiptId: string) : Flow<unit, CheckoutError, unit> =
    flow {
        do! Flow.sleep (TimeSpan.FromMilliseconds 20)
        return ()
    }

let notifyCustomer (receiptId: string) : Flow<unit, CheckoutError, unit> =
    flow {
        do! Flow.sleep (TimeSpan.FromMilliseconds 20)
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
    |> Flow.timeoutToError (TimeSpan.FromMilliseconds 10) CheckoutTimedOut
```

`timeout`, `timeoutToError`, `timeoutToOk`, and `timeoutWith` are boundary tools. They answer "what should this workflow do if it takes too long?"

## Retry

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
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

`Flow.retry` takes a `Schedule`, which sees each typed error: `Schedule.whileInput` selects the errors worth retrying,
and `Schedule.recursAtMost` bounds the attempts. For the common case, `Retry.schedule` builds the same schedule from a record
with named fields.

## Exceptions

```fsharp
let rawGatewayCall : Flow<unit, CheckoutError, string> =
    flow {
        if DateTime.UtcNow.Second % 2 = 0 then
            return raise (InvalidOperationException "gateway client exploded")

        return "receipt-123"
    }

let safeGatewayCall =
    rawGatewayCall
    |> Flow.catch (fun ex -> UnexpectedGatewayFailure ex.Message)
```

Use `Flow.catch` when you are deliberately translating technical exceptions into your typed error channel. If you do not catch them, they surface as `Cause.Die` in the final `Exit`.

## Cancellation

```fsharp
let runCancellable (cancellationToken: CancellationToken) =
    task {
        let! exit = checkoutWithTimeout.StartAsTask((), cancellationToken = cancellationToken)

        match exit with
        | Exit.Success receipt -> printfn "Receipt %s" receipt
        | Exit.Failure Cause.Interrupt -> printfn "Cancelled"
        | Exit.Failure cause -> printfn "%s" (Cause.prettyPrint string cause)
    }
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

```fsharp no-check reason="Shown independently; surrounding application context is intentionally omitted"
let annotatedCharge =
    flow {
        let! annotations = Flow.annotations
        let! traceId = Flow.traceId
        return annotations, traceId
    }
```

Annotations are useful for observability and correlation. They belong to runtime mechanics, not to your domain model.

## Pulling It Together

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let guardedCheckout =
    safeGatewayCall
    |> Flow.bind (fun receiptId ->
        flow {
            do! storeReceipt receiptId
            do! notifyCustomer receiptId
            return receiptId
        })
    |> Flow.timeoutToError (TimeSpan.FromSeconds 2) CheckoutTimedOut
    |> Flow.retry (
        Retry.schedule
            { Retry.defaults with
                When =
                    function
                    | GatewayUnavailable
                    | ReceiptStoreFailed -> true
                    | _ -> false })
```

Each concern has its own place:

- domain validation decides whether the operation should happen at all
- runtime helpers decide how the host should run that operation
- `Flow.catch` decides which technical exceptions should become typed failures
