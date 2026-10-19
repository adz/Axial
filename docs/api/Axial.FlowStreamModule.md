# FlowStream

`FlowStream<'env, 'error, 'value>` describes a cold, pull-based sequence of effectful values. It uses the same
environment, typed failure channel, cancellation, and runtime scopes as `Flow`.

Choose `FlowStream` when values should be handled incrementally instead of collected before work begins. Typical
sources include process output, paginated APIs, subscriptions, and host-specific file or network adapters.

A stream does nothing until a terminal operation such as [`runFold`](#Axial.FlowStream.runFold),
[`runForEachFlow`](#Axial.FlowStream.runForEachFlow), or
[`runCollect`](#Axial.FlowStream.runCollect) produces a Flow and that Flow is run. Pulling provides backpressure: downstream requests each next value, so upstream cannot run ahead
without an operator that introduces buffering. [`buffer`](#Axial.FlowStream.buffer) does that explicitly, running
upstream in its own fiber and buffering values with the `QueueStrategy` you choose, so upstream may run ahead up to
that capacity. The time-based operators ([`groupedWithin`](#Axial.FlowStream.groupedWithin),
[`throttle`](#Axial.FlowStream.throttle), [`debounce`](#Axial.FlowStream.debounce), and
[`switchMapFlow`](#Axial.FlowStream.switchMapFlow)) do not buffer: they pull at most one value ahead into a one-slot
queue (which keeps upstream back-pressure) so they can wait for the next value or a timer, then group values, replace a
pending value, or interrupt the previous flow when a newer value arrives.

A stream that reads a file, socket, or cursor should own it with [`using`](#Axial.FlowStream.using), which releases
the resource however consumption ends. To take a single result without draining the stream, use
[`runTryHead`](#Axial.FlowStream.runTryHead).

Streams connect to the concurrency types in both directions. [`fromDequeue`](#Axial.FlowStream.fromDequeue) reads a
queue or hub subscription until it is shut down and drained, and [`fromHub`](#Axial.FlowStream.fromHub) subscribes for
the life of the stream. [`runIntoQueue`](#Axial.FlowStream.runIntoQueue) and [`runIntoHub`](#Axial.FlowStream.runIntoHub)
feed a stream into them. [`mergePar`](#Axial.FlowStream.mergePar) runs several streams concurrently, and
[`fromSchedule`](#Axial.FlowStream.fromSchedule) turns a schedule into a tick stream. See the [Queue](/concurrency-and-state/queue.html)
and [Hub](/concurrency-and-state/hub.html) guides.

A minimal pipeline is a source, an operator, and a terminal run as a Flow:

```fsharp prepare
open Axial

let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
let names : Flow<unit, Never, string list> =
    FlowStream.fromSeq [ "ada"; "grace" ]
    |> FlowStream.map (fun name -> name.ToUpperInvariant())
    |> FlowStream.runCollect
```

```fsharp run
names |> Flow.run () |> shouldEqual (Exit.Success [ "ADA"; "GRACE" ])
```

## Guides

The [Streams guide](/streams/index.html) introduces the model. [Getting started](/streams/getting-started.html) builds a
complete pipeline, while [Batching and parallelism](/streams/batching-and-parallelism.html) explains the scheduling
difference between strict batches and [`mapFlowPar`](#Axial.FlowStream.mapFlowPar).

Use incremental terminal operations for large or unbounded streams. [`runCollect`](#Axial.FlowStream.runCollect) retains every emitted
value and should be reserved for known finite inputs.
