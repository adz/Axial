---
title: Transforming Values
---

# Transforming Values

Pure operators transform one pulled value without introducing another effect:

```fsharp transcript
> (FlowStream.fromSeq [ 1..20 ] : FlowStream<int>)
- |> FlowStream.filter (fun value -> value % 2 = 0)
- |> FlowStream.map (fun value -> value * 10)
- |> FlowStream.skip 2
- |> FlowStream.take 3
- |> FlowStream.runCollect
- |> Flow.run ();;
val it: Exit<int list,Never> = Success [60; 80; 100]
```

`FlowStream.choose` combines filtering and mapping. `FlowStream.indexed`, `FlowStream.scan`, and `FlowStream.distinctUntilChangedBy` retain only the state needed for
the next result. `FlowStream.takeWhile` and `FlowStream.skipWhile` stop or change behavior according to the first matching value.

## Effectful transformations

`FlowStream.mapFlow` runs one Flow for each value and emits its result:

```fsharp transcript
> (FlowStream.fromSeq [ "a"; "b" ] : FlowStream<string>)
- |> FlowStream.mapFlow (fun letter -> Flow.succeed (letter.ToUpperInvariant()))
- |> FlowStream.runCollect
- |> Flow.run ();;
val it: Exit<string list,Never> = Success ["A"; "B"]
```

`FlowStream.tapFlow` runs an effect but preserves the original value. The example uses `Flow.delay` so printing happens when the stream pulls the value, not when the pipeline is constructed:

```fsharp transcript
> let observed = ResizeArray<int>() in
- (FlowStream.fromSeq [ 1; 2 ] : FlowStream<int>)
- |> FlowStream.tapFlow (fun number ->
-     Flow.delay (fun () ->
-         observed.Add number
-         Flow.succeed ()))
- |> FlowStream.map (fun number -> number * 10)
- |> FlowStream.runCollect
- |> Flow.map (fun emitted -> List.ofSeq observed, emitted)
- |> Flow.run ();;
val observed: List<int> = seq [1; 2]
val it: Exit<Tuple<int list,int list>,Never> = Success ([1; 2], [10; 20])
```

Both operators remain sequential. They do not pull the next value until the current mapping has completed and the
consumer asks for another result. Use [Batching and parallelism](batching-and-parallelism.html) when mappings should
overlap.

## Errors and cancellation

`FlowStream.mapError` changes only the typed failure channel. Defects and interruption remain structurally distinct. When an
operator fails, downstream receives that cause and no further upstream values are pulled.
