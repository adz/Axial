---
title: Constructing Streams
---

# Constructing Streams

An array or `seq` is the right source when all values already exist and producing the next value is synchronous. Wrap one in a `FlowStream` to compose it with effectful sources and incremental consumers under one
failure, cancellation, backpressure, and cleanup model.

Use `FlowStream.fromSeq`, `FlowStream.singleton`, and `FlowStream.empty` for those existing values:

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

```fsharp transcript
> (FlowStream.fromSeq [ 1..100 ] : FlowStream<int>)
- |> FlowStream.take 3
- |> FlowStream.append (FlowStream.singleton 42)
- |> FlowStream.runCollect
- |> Flow.run ();;
val it: Exit<int list,Never> = Success [1; 2; 3; 42]
```

`FlowStream.empty` emits nothing. `FlowStream.fromSeq` obtains its enumerator only when consumption starts. The enumerator is registered with the stream's Flow
scope and disposed after completion or early termination.

## Bridge callbacks

`FlowStream.fromCallback capacity subscribe` creates a bounded stream from a synchronous callback. The subscription
returns an `IDisposable` in a Flow; its disposal runs when consumption completes, fails, stops early, or is interrupted.
If subscription attaches a handler before a later step can fail, register its cleanup with `Flow.scopeFinalizer`.
`Emit.Value` returns `false` when the buffer is full (dropping the newest value) or the stream has ended. `Emit.Fail`
ends with a typed error; `Emit.End` completes normally. Later callbacks are ignored. Use
`FlowStream.fromCallbackWith capacity (CallbackOverflow.Fail error) subscribe` to fail rather than drop on overflow:
previously queued values are delivered before the failure.

```fsharp transcript
> open System;;
> open System.Threading.Tasks;;
> open Axial;;
> let notifications () : FlowStream<unit, string, int> = FlowStream.fromCallback 3 (fun emit -> Flow.delay (fun () -> Task.Run(fun () -> [1; 2; 3] |> List.iter (fun i -> emit.Value i |> ignore); emit.End()) |> ignore; Flow.ok ({ new IDisposable with member _.Dispose() = () })));;
> notifications () |> FlowStream.runCollect |> Flow.run ();;
val it: Exit<int list,string> = Success [1; 2; 3]
```

## Lift one effect

`FlowStream.fromFlow` creates a stream containing the successful result of one Flow:

```fsharp transcript
> (Flow.succeed "Ada" : Flow<string>)
- |> FlowStream.fromFlow
- |> FlowStream.runCollect
- |> Flow.run ();;
val it: Exit<string list,Never> = Success ["Ada"]
```

A failed Flow fails the stream before producing a value.

## Unfold effectful state

`FlowStream.unfoldFlow` repeatedly runs an effectful state transition. Return `Some(value, nextState)` to emit a value
and remember the state for the next pull, or return `None` to finish.

The value goes downstream; the state stays inside the source and tells it how to continue. A paginated source might emit a page of messages while retaining an
opaque continuation token. A socket parser might emit a decoded frame while retaining unread bytes. Requiring only a
next state would either emit implementation state to consumers or restrict unfolding to sources whose state happens to
be their output.

```fsharp transcript
> (1
-  |> FlowStream.unfoldFlow (fun number ->
-      Flow.succeed (
-          if number > 3 then None
-          else Some(number, number + 1)))
-  : FlowStream<int>)
- |> FlowStream.runCollect
- |> Flow.run ();;
val it: Exit<int list,Never> = Success [1; 2; 3]
```

Only one step runs per downstream pull. In this small numeric example the emitted value and next state look similar,
but they are independent parts of the transition.

## Build a concurrent crawler

For a source that discovers its own work — a link crawler or a paginated API — combine `unfoldFlow` with
`mapFlowPar` and `collect`. The [crawler recipe](crawler-recipe.html) builds one end to end: a private frontier held
in the unfold state, overlapping fetches, and backpressure between batches.
