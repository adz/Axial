---
weight: 60
title: FlowStream
description: Cold, pull-based, effectful streams for .NET and Fable.
---

# FlowStream

This page shows how to construct, transform, and consume cold streams that share Flow's environment, typed failures,
cancellation, and platform portability.

```fsharp
FlowStream<'env, 'error, 'value>
```

Each pull produces one value or completes. The consumer requests the next value, so producers cannot outrun consumers.
The implementation uses Axial's `Execution` abstraction rather than `IAsyncEnumerable`; the same model works on .NET
and Fable.

## Construct Streams

Use `fromSeq`, `singleton`, or `empty` for existing values:

```fsharp
let numbers = FlowStream.fromSeq [ 1..100 ]
let one = FlowStream.singleton 42
let none : FlowStream<unit, string, int> = FlowStream.empty
```

Lift one effect with `fromFlow`, or build an asynchronous state machine with `unfoldFlow`:

```fsharp
let pages =
    FlowStream.unfoldFlow
        (fun page ->
            flow {
                let! response = fetchPage page
                return
                    if response.Items.IsEmpty then None
                    else Some(response.Items, page + 1)
            })
        1
```

`unfoldFlow` is the integration point for sockets, message subscriptions, paginated APIs, and platform adapters. The
step remains a normal `Flow`, so dependencies and failures are explicit.

## Transform Values

```fsharp
let selected =
    numbers
    |> FlowStream.filter (fun value -> value % 2 = 0)
    |> FlowStream.map (fun value -> value * 10)
    |> FlowStream.skip 2
    |> FlowStream.take 3
```

`choose` combines filtering and mapping. `mapError` changes the typed failure channel. `mapFlow` performs an effectful
transformation, while `tapFlow` performs an effect and preserves the original value:

```fsharp
let enriched =
    ids
    |> FlowStream.mapFlow loadCustomer
    |> FlowStream.tapFlow (fun customer -> Log.info $"loaded {customer.Id}")
```

## Compose Streams

`append` evaluates the right stream only after the left completes. `collect` maps each value to a stream and flattens
them in order. `zip` stops when either side completes:

```fsharp
let values =
    FlowStream.fromSeq [ 1; 2 ]
    |> FlowStream.append (FlowStream.singleton 3)
    |> FlowStream.collect (fun value -> FlowStream.fromSeq [ value; value * 10 ])
    |> FlowStream.zip (FlowStream.fromSeq [ "a"; "b"; "c"; "d"; "e"; "f" ])
```

## Consume Inside Flow

Consumers return an ordinary Flow. The environment is supplied once, when that Flow runs:

```fsharp
let collected : Flow<AppEnv, LoadError, int list> =
    selected |> FlowStream.runCollect

let total : Flow<AppEnv, LoadError, int> =
    selected |> FlowStream.runFold (+) 0

let printAll : Flow<AppEnv, LoadError, unit> =
    selected |> FlowStream.runForEach (printfn "%d")

let saveAll : Flow<AppEnv, LoadError, unit> =
    customers |> FlowStream.runForEachFlow saveCustomer
```

Use `runDrain` when only producer effects matter. `runCollect` intentionally loads every value; prefer a fold or
incremental consumer for unbounded streams.

## Process Output

`Axial.Flow.Process.Process.stream` is a concrete example of an effectful, backpressured source. It emits structured
stdout/stderr events followed by a completion transcript and cancels the child pipeline if stream consumption stops.
See [Output and streaming](processes/output-streaming/).

## Platform Boundary

All `FlowStream` functions on this page are Fable-compatible. Platform-specific producers should implement their I/O
adapter outside `Axial.Flow`; only executor mechanics belong in `Platform.fs`. For example, Node child-process launching
belongs in a process adapter package, while the resulting values still compose through this same stream API.
