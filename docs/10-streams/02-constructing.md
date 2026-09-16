---
title: Constructing Streams
---

# Constructing Streams

Use `fromSeq`, `singleton`, and `empty` for values already available in memory:

```fsharp
let numbers : FlowStream<unit, Never, int> = FlowStream.fromSeq [ 1..100 ]
let one : FlowStream<unit, Never, int> = FlowStream.singleton 42
let none : FlowStream<unit, string, int> = FlowStream.empty
```

`fromSeq` obtains its enumerator only when consumption starts. The enumerator is registered with the stream's Flow
scope and disposed after completion or early termination.

## Lift one effect

`fromFlow` creates a stream containing the successful result of one Flow:

```fsharp no-check reason="Application-specific operation is described in the surrounding prose"
let currentUser =
    loadCurrentUser
    |> FlowStream.fromFlow
```

A failed Flow fails the stream before producing a value.

## Unfold effectful state

`unfoldFlow` repeatedly runs an effectful state transition. Return `Some(value, nextState)` to emit a value or `None`
to finish:

```fsharp no-check reason="Application-specific pagination types are intentionally omitted"
let pages =
    FlowStream.unfoldFlow
        (fun pageNumber ->
            flow {
                let! response = fetchPage pageNumber
                return
                    if response.Items.IsEmpty then None
                    else Some(response.Items, pageNumber + 1)
            })
        1
```

Only one step runs per downstream pull. This makes `unfoldFlow` the basic integration point for paginated APIs,
sockets, subscriptions, and other host adapters without moving their I/O into Axial core.
