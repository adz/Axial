---
title: Composing Streams
---

# Composing Streams

Composition builds a larger stream without starting any source.

## Append

`FlowStream.append` consumes the left stream before the right stream:

```fsharp transcript
> (FlowStream.fromSeq [ 1; 2 ] : FlowStream<int>)
- |> FlowStream.append (FlowStream.fromSeq [ 3; 4 ])
- |> FlowStream.runCollect
- |> Flow.run ();;
val it: Exit<int list,Never> = Success [1; 2; 3; 4]
```

The right side is not pulled while the left side still has values.

## Map and flatten

`FlowStream.collect` maps each value to an inner stream and flattens those streams in order. This is the stream equivalent of
`flatMap` or monadic `bind`:

```fsharp transcript
> (FlowStream.fromSeq [ 1; 2; 3 ] : FlowStream<int>)
- |> FlowStream.collect (fun value ->
- FlowStream.fromSeq [ value; value * 10 ])
- |> FlowStream.runCollect
- |> Flow.run ();;
val it: Exit<int list,Never> = Success [1; 10; 2; 20; 3; 30]
```

Each inner stream completes before the next outer value is expanded.

## Zip

`FlowStream.zip` pulls one value from each side and stops when either side completes:

```fsharp transcript
> (FlowStream.fromSeq [ 1; 2; 3 ] : FlowStream<int>)
- |> FlowStream.zip (FlowStream.fromSeq [ "a"; "b" ])
- |> FlowStream.runCollect
- |> Flow.run ();;
val it: Exit<Tuple<int,string> list,Never> = Success [(1, "a"); (2, "b")]
```

The result contains two pairs. The remaining value from the longer side is not emitted.

## Lifetimes inside composition

A composed stream may own several upstream resources. Natural completion closes a source when it finishes, while the
terminal consumer's child scope is the final safety boundary for failure, interruption, and early termination. See
[Scopes](/scopes/index.html) for the ownership model.
