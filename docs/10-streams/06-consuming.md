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

Use it only when the stream is known to be finite and the complete list is required. It intentionally retains every
value until completion.

## The terminal consumer owns the stream scope

You do not normally wrap stream consumption in `Flow.scoped`. Every terminal operation—`FlowStream.runFold`,
`FlowStream.runForEach`, `FlowStream.runForEachFlow`, `FlowStream.runCollect`, and `FlowStream.runDrain`—automatically
creates a child scope when its returned Flow starts running.

The lifecycle is:

1. Building a `FlowStream` opens nothing and creates no scope.
2. Running the terminal Flow creates the stream's child scope.
3. Pulling starts the source. Enumerators, adapter handles, and parallel mapping fibers register with that scope.
4. Operators and their subflows use the same stream scope unless they explicitly call `Flow.scoped`.
5. The terminal consumer stops pulling after completion, failure, interruption, or an operator such as
   `FlowStream.take` ending early.
6. It closes the child scope, runs finalizers in reverse registration order, interrupts and awaits unfinished child
   fibers, and only then returns its `Exit`.

This means an enumerator opened by `FlowStream.fromSeq` is disposed before `FlowStream.runCollect` returns. Likewise,
when `FlowStream.mapFlowPar` has outstanding mappings and downstream stops after `FlowStream.take`, the terminal scope
interrupts and awaits those mappings before the terminal Flow completes.

A Flow run by `FlowStream.mapFlow`, `FlowStream.tapFlow`, or `FlowStream.runForEachFlow` can use `Flow.scopeResource` or
another `Flow.scope...` operation. That resource then belongs to the terminal stream scope and remains alive until the
whole terminal operation finishes—not merely until that one mapping returns. Add `Flow.scoped` inside the mapping only
when each mapped value needs a shorter, per-value cleanup boundary.

An additional outer `Flow.scoped` is useful only when the application intentionally groups the terminal Flow with
other work or resources. It is not required for stream safety. The terminal Flow still uses the source's environment
and typed error channel, so supply the environment once at the application edge.
