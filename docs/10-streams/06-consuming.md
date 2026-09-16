---
title: Consuming Streams
---

# Consuming Streams

A stream is only a description until a terminal consumer turns it into a `Flow`. Running that Flow opens the source and
starts pulling.

## Incremental consumers

Prefer incremental consumers when the stream may be large or unbounded:

```fsharp
let total : Flow<unit, Never, int> =
    FlowStream.fromSeq [ 1..100 ]
    |> FlowStream.runFold (+) 0

let printAll : Flow<unit, Never, unit> =
    FlowStream.fromSeq [ 1..100 ]
    |> FlowStream.runForEach (printfn "%d")
```

Use `runForEachFlow` when handling each value is effectful:

```fsharp no-check reason="Application-specific save operation is described in the surrounding prose"
let saveAll =
    customers
    |> FlowStream.runForEachFlow saveCustomer
```

`runDrain` ignores emitted values while preserving producer effects.

## Collect finite streams

`runCollect` returns all values as a list:

```fsharp
let values : Flow<unit, Never, int list> =
    FlowStream.fromSeq [ 1; 2; 3 ]
    |> FlowStream.runCollect
```

Use it only when the stream is known to be finite and the complete list is required. It intentionally retains every
value until completion.

## Terminal scope

Every terminal consumer runs stream consumption in a child Flow scope. Before returning, it closes that scope and
waits for registered cleanup. This covers:

- successful completion;
- typed failure or defect;
- external interruption;
- early completion from operators such as `take`;
- outstanding child fibers created by parallel mappings.

The terminal Flow still uses the same environment and typed error channel as its source. Supply the environment once at
the application edge.
