---
title: Composing Streams
---

# Composing Streams

Composition builds a larger stream without starting any source.

## Append

`append` consumes the left stream before the right stream:

```fsharp
let combined : FlowStream<unit, Never, int> =
    FlowStream.fromSeq [ 1; 2 ]
    |> FlowStream.append (FlowStream.fromSeq [ 3; 4 ])

combined
|> FlowStream.runForEach (printfn "%d")
|> Flow.run ()
|> ignore
```

```text
1
2
3
4
```

The right side is not pulled while the left side still has values.

## Map and flatten

`collect` maps each value to an inner stream and flattens those streams in order. This is the stream equivalent of
`flatMap` or monadic `bind`:

```fsharp
let expanded : FlowStream<unit, Never, int> =
    FlowStream.fromSeq [ 1; 2; 3 ]
    |> FlowStream.collect (fun value ->
        FlowStream.fromSeq [ value; value * 10 ])

expanded
|> FlowStream.runForEach (printfn "%d")
|> Flow.run ()
|> ignore
```

```text
1
10
2
20
3
30
```

Each inner stream completes before the next outer value is expanded.

## Zip

`zip` pulls one value from each side and stops when either side completes:

```fsharp
let labelled : FlowStream<unit, Never, int * string> =
    FlowStream.fromSeq [ 1; 2; 3 ]
    |> FlowStream.zip (FlowStream.fromSeq [ "a"; "b" ])

labelled
|> FlowStream.runForEach (fun (number, letter) ->
    printfn "%d%s" number letter)
|> Flow.run ()
|> ignore
```

```text
1a
2b
```

The result contains two pairs. The remaining value from the longer side is not emitted.

## Lifetimes inside composition

A composed stream may own several upstream resources. Natural completion closes a source when it finishes, while the
terminal consumer's child scope is the final safety boundary for failure, interruption, and early termination. See
[Scopes](/scopes/index.html) for the ownership model.
