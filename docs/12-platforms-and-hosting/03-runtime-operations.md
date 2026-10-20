---
title: "Tutorial: Runtime Operations"
description: Timeout, retry, cancellation, and runtime annotations.
---

# Tutorial: Runtime Operations

Use timeout, retry, cancellation, annotations, and exception translation at the application boundary, not in place of domain rules.

## A Small Workflow To Wrap

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
open System.Threading

type CheckoutError =
    | GatewayUnavailable
    | CheckoutTimedOut
    | UnexpectedGatewayFailure of string

/// One small workflow the runtime helpers below wrap: slow, cancellable, and able to fail.
let checkout : Flow<ClockEnvironment, CheckoutError, string> =
    flow {
        do! Flow.sleep (TimeSpan.FromMilliseconds 50.0)
        return "receipt-123"
    }
```

Runtime helpers control what happens when this workflow is slow, fails, is canceled, or throws. The
[scheduling guide](/scheduling-and-retries/index.html) covers the schedule combinators in full.

## Timeout

```fsharp
let checkoutWithTimeout =
    checkout
    |> Flow.timeoutToError (TimeSpan.FromMilliseconds 10.0) CheckoutTimedOut
```

Authorizing the card takes 50 ms, so the 10 ms limit fails the checkout:

```fsharp run
checkoutWithTimeout |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Failure(Cause.Fail CheckoutTimedOut))
```

`timeout`, `timeoutToError`, `timeoutToOk`, and `timeoutWith` set the outcome when a workflow takes too long.

## Retry

```fsharp
let retryingCheckout =
    checkout
    |> Flow.retry (
        Schedule.exponential (TimeSpan.FromMilliseconds 50.0)
        |> Schedule.recursAtMost 3
        |> Schedule.whileInput (function
            | GatewayUnavailable -> true
            | _ -> false))
```

```fsharp run
retryingCheckout |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success "receipt-123")
```

`Flow.retry` takes a `Schedule`, which sees each typed error: `Schedule.whileInput` selects the errors worth retrying,
and `Schedule.recursAtMost` bounds the attempts. `Retry.schedule` builds the same schedule from a record with named fields.

## Exceptions

```fsharp
let rawGatewayCall (clientExplodes: bool) : Flow<ClockEnvironment, CheckoutError, string> =
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
|> Flow.run (ClockEnvironment Clock.live)
|> shouldEqual (Exit.Failure(Cause.Fail(UnexpectedGatewayFailure "gateway client exploded")))
```

Use `Flow.catch` when you are deliberately translating technical exceptions into your typed error channel. If you do not catch them, they surface as `Cause.Die` in the final `Exit`.

## Cancellation

```fsharp
let runCancellable (cancellationToken: CancellationToken) =
    task {
        let! exit = checkout.StartAsTask(ClockEnvironment Clock.live, cancellationToken = cancellationToken)

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
let annotatedCharge : Flow<ClockEnvironment, CheckoutError, Map<string, string> * string option> =
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
|> Flow.run (ClockEnvironment Clock.live)
|> shouldEqual (Exit.Success(Map [ "order_id", "o-7"; "trace_id", "trace-1" ], Some "trace-1"))
```

Annotations carry observability and correlation data at runtime, outside the domain model.

## Combine the Helpers

```fsharp
let guardedCheckout =
    safeGatewayCall
    |> Flow.timeoutToError (TimeSpan.FromSeconds 2.0) CheckoutTimedOut
    |> Flow.retry (
        Retry.schedule
            { Retry.defaults with
                When = (function | GatewayUnavailable -> true | _ -> false) })
```

```fsharp run
guardedCheckout |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success "receipt-123")
```

The resulting workflow keeps these decisions separate:

- domain validation decides whether the operation should happen at all
- runtime helpers decide how the host should run that operation
- `Flow.catch` decides which technical exceptions should become typed failures
