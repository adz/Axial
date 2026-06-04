---
weight: 20
title: "Tutorial: Runtime Operations"
description: Timeout, retry, cancellation, and runtime annotations.
type: docs
---


This tutorial focuses on the operational helpers that sit around a workflow: timeout, retry, cancellation, annotations, and exception translation.

Use these helpers at the application boundary. They are not substitutes for domain rules.

## A Small Workflow To Wrap

```fsharp
open System
open System.Threading
open FsFlow

type CheckoutError =
    | GatewayUnavailable
    | CheckoutTimedOut
    | CheckoutCancelled
    | ReceiptStoreFailed
    | UnexpectedGatewayFailure of string

let authorizeCard : Flow<unit, CheckoutError, string> =
    flow {
        do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 50)
        return "receipt-123"
    }

let storeReceipt (receiptId: string) : Flow<unit, CheckoutError, unit> =
    flow {
        do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 20)
        return ()
    }

let notifyCustomer (receiptId: string) : Flow<unit, CheckoutError, unit> =
    flow {
        do! Flow.Runtime.sleep (TimeSpan.FromMilliseconds 20)
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
    |> Flow.Runtime.timeoutToError (TimeSpan.FromMilliseconds 10) CheckoutTimedOut
```

`timeout`, `timeoutToError`, `timeoutToOk`, and `timeoutWith` are boundary tools. They answer "what should this workflow do if it takes too long?"

## Retry

```fsharp
let retryingCheckout =
    checkout
    |> Flow.Runtime.retry (function
        | GatewayUnavailable -> Some (TimeSpan.FromMilliseconds 100)
        | ReceiptStoreFailed -> Some (TimeSpan.FromMilliseconds 50)
        | _ -> None)
```

Use `Flow.Runtime.retry` when the retry decision depends on the actual typed error. Use `Schedule` when you want a reusable retry policy value.

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
        let! exit = checkoutWithTimeout.ToTask((), cancellationToken = cancellationToken)

        match exit with
        | Exit.Success receipt -> printfn "Receipt %s" receipt
        | Exit.Failure Cause.Interrupt -> printfn "Cancelled"
        | Exit.Failure cause -> printfn "%s" (Cause.prettyPrint string cause)
    }
```

If the host cancels the token, the flow finishes with `Exit.Failure Cause.Interrupt`.

## Annotations

```fsharp
let annotatedCharge =
    flow {
        let! annotations = Flow.Runtime.annotations
        let! traceId = Flow.Runtime.traceId
        return annotations, traceId
    }
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
    |> Flow.Runtime.timeoutToError (TimeSpan.FromSeconds 2) CheckoutTimedOut
    |> Flow.Runtime.retry (function
        | GatewayUnavailable -> Some (TimeSpan.FromMilliseconds 200)
        | ReceiptStoreFailed -> Some (TimeSpan.FromMilliseconds 100)
        | _ -> None)
```

Keep the mental split clear:

- domain validation decides whether the operation should happen at all
- runtime helpers decide how the host should run that operation
- `Flow.catch` decides which technical exceptions should become typed failures
