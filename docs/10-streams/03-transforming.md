---
title: Transforming Values
---

# Transforming Values

Pure operators transform one pulled value without introducing another effect:

```fsharp
let selected : FlowStream<unit, Never, int> =
    FlowStream.fromSeq [ 1..20 ]
    |> FlowStream.filter (fun value -> value % 2 = 0)
    |> FlowStream.map (fun value -> value * 10)
    |> FlowStream.skip 2
    |> FlowStream.take 3
```

`choose` combines filtering and mapping. `indexed`, `scan`, and `distinctUntilChangedBy` retain only the state needed for
the next result. `takeWhile` and `skipWhile` stop or change behavior according to the first matching value.

## Effectful transformations

`mapFlow` runs one Flow for each value and emits its result:

```fsharp no-check reason="Application-specific customer operation is described in the surrounding prose"
let customers =
    customerIds
    |> FlowStream.mapFlow loadCustomer
```

`tapFlow` runs an effect but preserves the original value:

```fsharp no-check reason="Application-specific logging operation is described in the surrounding prose"
let observed =
    customers
    |> FlowStream.tapFlow (fun customer -> Log.info $"loaded {customer.Id}")
```

Both operators remain sequential. They do not pull the next value until the current mapping has completed and the
consumer asks for another result. Use [Batching and parallelism](batching-and-parallelism.html) when mappings should
overlap.

## Errors and cancellation

`mapError` changes only the typed failure channel. Defects and interruption remain structurally distinct. When an
operator fails, downstream receives that cause and no further upstream values are pulled.
