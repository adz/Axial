---
title: Streams
description: Cold, pull-based, effectful streams for .NET and Fable.
---

# Streams

This page shows how to construct, transform, and consume cold streams that share Flow's environment, typed failures,
cancellation, and platform portability.

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
FlowStream<'env, 'error, 'value>
```

Each pull produces one value or completes. The consumer requests the next value, so producers cannot outrun consumers.
The implementation uses Axial's `Execution` abstraction rather than `IAsyncEnumerable`; the same model works on .NET
and Fable.

## Construct Streams

Use `fromSeq`, `singleton`, or `empty` for existing values:

```fsharp no-check reason="Shown independently; surrounding application context is intentionally omitted"
let numbers = FlowStream.fromSeq [ 1..100 ]
let one = FlowStream.singleton 42
let none : FlowStream<unit, string, int> = FlowStream.empty
```

Lift one effect with `fromFlow`, or build an asynchronous state machine with `unfoldFlow`:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
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

```fsharp no-check reason="Shown independently; surrounding application context is intentionally omitted"
let selected =
    numbers
    |> FlowStream.filter (fun value -> value % 2 = 0)
    |> FlowStream.map (fun value -> value * 10)
    |> FlowStream.skip 2
    |> FlowStream.take 3
```

`choose` combines filtering and mapping. `mapError` changes the typed failure channel. `mapFlow` performs an effectful
transformation, while `tapFlow` performs an effect and preserves the original value:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
let enriched =
    ids
    |> FlowStream.mapFlow loadCustomer
    |> FlowStream.tapFlow (fun customer -> Log.info $"loaded {customer.Id}")
```

Use `chunkBySize` when downstream work should receive bounded non-empty batches. Compose it with
`Flow.collectAllPar` when every batch should run concurrently and finish before the next batch starts:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
let checkedPages =
    pages
    |> FlowStream.chunkBySize 2
    |> FlowStream.mapFlow (List.map checkAndExtract >> Flow.collectAllPar)
```

This composition pulls and retains at most two source values, preserves input order, and does not pull the next batch
until the current mapped results are consumed. If one mapping fails, `Flow.collectAllPar` interrupts the other mappings
in that batch.

Use `mapFlowPar` when work should be continuously replenished instead of separated by strict batch barriers:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
let loaded =
    ids
    |> FlowStream.mapFlowPar (Parallelism.bounded 8) loadCustomer
```

`mapFlowPar` keeps at most the configured number of child mappings active or retained. Results are emitted in completion
order, so a slow earlier item does not block later results or failures. Consuming one result opens capacity and starts
the next upstream mapping. Use explicit `chunkBySize` plus `Flow.collectAllPar` when input order and strict batch
barriers are required.

## Compose Streams

`append` evaluates the right stream only after the left completes. `collect` maps each value to a stream and flattens
them in order. `zip` stops when either side completes:

```fsharp no-check reason="Shown independently; surrounding application context is intentionally omitted"
let values =
    FlowStream.fromSeq [ 1; 2 ]
    |> FlowStream.append (FlowStream.singleton 3)
    |> FlowStream.collect (fun value -> FlowStream.fromSeq [ value; value * 10 ])
    |> FlowStream.zip (FlowStream.fromSeq [ "a"; "b"; "c"; "d"; "e"; "f" ])
```

## Consume Inside Flow

Consumers return an ordinary Flow. The environment is supplied once, when that Flow runs:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
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

`Axial.Process.Process.stream` is a concrete example of an effectful, backpressured source. It emits structured
stdout/stderr events followed by a completion transcript and cancels the child pipeline if stream consumption stops.
See [Streaming output](/process/streaming.html).

## Platform Boundary

All `FlowStream` functions on this page are Fable-compatible. Platform-specific producers should implement their I/O
adapter outside `Axial`; only executor mechanics belong in `Platform.fs`. For example, Node child-process launching
belongs in a process adapter package, while the resulting values still compose through this same stream API.
