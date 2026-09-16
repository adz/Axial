---
title: Constructing Streams
---

# Constructing Streams

Use `fromSeq`, `singleton`, and `empty` for values already available in memory:

```fsharp
let numbers : FlowStream<unit, Never, int> = FlowStream.fromSeq [ 1..100 ]
let one : FlowStream<unit, Never, int> = FlowStream.singleton 42
let none : FlowStream<unit, string, int> = FlowStream.empty

numbers
|> FlowStream.take 3
|> FlowStream.runForEach (printfn "number %d")
|> Flow.run ()
|> ignore

one
|> FlowStream.runForEach (printfn "one %d")
|> Flow.run ()
|> ignore
```

```text
number 1
number 2
number 3
one 42
```

`empty` emits nothing. `fromSeq` obtains its enumerator only when consumption starts. The enumerator is registered with the stream's Flow
scope and disposed after completion or early termination.

## Lift one effect

`fromFlow` creates a stream containing the successful result of one Flow:

```fsharp
Flow.succeed "Ada"
|> FlowStream.fromFlow
|> FlowStream.runForEach (printfn "user: %s")
|> Flow.run ()
|> ignore
```

```text
user: Ada
```

A failed Flow fails the stream before producing a value.

## Unfold effectful state

`unfoldFlow` repeatedly runs an effectful state transition. Return `Some(value, nextState)` to emit a value or `None`
to finish:

```fsharp
FlowStream.unfoldFlow
    (fun number ->
        Flow.succeed (
            if number > 3 then None
            else Some(number, number + 1)))
    1
|> FlowStream.runForEach (printfn "page %d")
|> Flow.run ()
|> ignore
```

```text
page 1
page 2
page 3
```

Only one step runs per downstream pull. This makes `unfoldFlow` the basic integration point for paginated APIs,
sockets, subscriptions, and other host adapters without moving their I/O into Axial core.
