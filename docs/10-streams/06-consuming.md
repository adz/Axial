---
title: Consuming Streams
---

# Consuming Streams

A stream is only a description until a terminal consumer turns it into a `Flow`. Running that Flow opens the source and
starts pulling.

## Incremental consumers

Prefer incremental consumers when the stream may be large or unbounded:

```fsharp transcript
> (FlowStream.fromSeq [ 1..100 ] : FlowStream<int>)
- |> FlowStream.runFold (+) 0
- |> Flow.run ();;
val it: Exit<int,Never> = Success 5050
```

`FlowStream.runForEach` exposes each value without retaining the complete stream:

```fsharp transcript
> let observed = ResizeArray<int>() in
- (FlowStream.fromSeq [ 1; 2; 3 ] : FlowStream<int>)
- |> FlowStream.runForEach observed.Add
- |> Flow.map (fun () -> List.ofSeq observed)
- |> Flow.run ();;
val observed: List<int> = seq [1; 2; 3]
val it: Exit<int list,Never> = Success [1; 2; 3]
```

Use `FlowStream.runForEachFlow` when handling each value is effectful:

```fsharp transcript
> let saved = ResizeArray<string>() in
- (FlowStream.fromSeq [ "A"; "B" ] : FlowStream<string>)
- |> FlowStream.runForEachFlow (fun letter ->
-     Flow.delay (fun () ->
-         saved.Add letter
-         Flow.succeed ()))
- |> Flow.map (fun () -> List.ofSeq saved)
- |> Flow.run ();;
val saved: List<string> = seq ["A"; "B"]
val it: Exit<string list,Never> = Success ["A"; "B"]
```

`FlowStream.runDrain` ignores emitted values while preserving producer effects.

## Collect finite streams

`FlowStream.runCollect` returns all values as a list:

```fsharp transcript
> (FlowStream.fromSeq [ 1; 2; 3 ] : FlowStream<int>)
- |> FlowStream.runCollect
- |> Flow.run ();;
val it: Exit<int list,Never> = Success [1; 2; 3]
```

Use it only when the stream is known to be finite and the complete list is required. It retains every value until completion.

## The terminal consumer owns the stream scope

You do not normally wrap stream consumption in `Flow.scoped`. Every terminal operation (`FlowStream.runFold`,
`FlowStream.runForEach`, `FlowStream.runForEachFlow`, `FlowStream.runCollect`, and `FlowStream.runDrain`) automatically
creates a child scope when its returned Flow starts running, and closes it — running finalizers in reverse order and
interrupting and awaiting unfinished child fibers — before returning its `Exit`. An outer `Flow.scoped` is only for
grouping the terminal Flow with other work; it is not required for stream safety.

The full six-step lifecycle and the per-operator scope-ownership rules are in the
[scopes guide](/scopes/index.html#streams).
