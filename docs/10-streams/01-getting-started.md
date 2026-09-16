---
title: Getting Started
---

# Getting Started

A stream pipeline has three parts:

1. A source describes where values come from.
2. Operators transform values one pull at a time.
3. A terminal consumer turns the stream back into a `Flow` that can run.

```fsharp
let total : Flow<unit, Never, int> =
    FlowStream.fromSeq [ 1..10 ]
    |> FlowStream.filter (fun value -> value % 2 = 0)
    |> FlowStream.map (fun value -> value * 10)
    |> FlowStream.runFold (+) 0

let result = total |> Flow.run ()
```

Constructing `total` does not enumerate the sequence. `Flow.run` starts the terminal Flow. `runFold` then pulls one
value at a time through `filter` and `map`, retaining only the running total.

## Keep effects in Flow

Use `mapFlow` when transforming one value requires an effect:

```fsharp no-check reason="Application-specific customer operations are described in the surrounding prose"
let saveActiveCustomers =
    customerIds
    |> FlowStream.mapFlow loadCustomer
    |> FlowStream.filter _.IsActive
    |> FlowStream.runForEachFlow saveCustomer
```

A load or save failure stops further pulls and remains in the stream's typed error channel. Cancellation reaches the
active operation through the enclosing Flow runtime.

## Choose the terminal operation deliberately

`runCollect` is convenient for a known finite stream, but it stores every value. Prefer `runFold`, `runForEach`, or
`runForEachFlow` when values can be handled incrementally.

Terminal consumers create a child scope. Completion, failure, interruption, and early termination close stream-owned
resources and child fibers before the terminal Flow returns.

Next, read [Constructing streams](constructing.html).
