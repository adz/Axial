---
title: Constructing Streams
---

# Constructing Streams

An array or `seq` is the right source when all values already exist and producing the next value is synchronous. Wrapping
one in a `FlowStream` becomes useful when it must compose with effectful sources and incremental consumers under one
failure, cancellation, backpressure, and cleanup model.

Use `FlowStream.fromSeq`, `FlowStream.singleton`, and `FlowStream.empty` for those existing values:

```fsharp transcript
> (FlowStream.fromSeq [ 1..100 ] : FlowStream<int>)
- |> FlowStream.take 3
- |> FlowStream.append (FlowStream.singleton 42)
- |> FlowStream.runCollect
- |> Flow.run ();;
val it: Exit<int list,Never> = Success [1; 2; 3; 42]
```

`FlowStream.empty` emits nothing. `FlowStream.fromSeq` obtains its enumerator only when consumption starts. The enumerator is registered with the stream's Flow
scope and disposed after completion or early termination.

## Lift one effect

`FlowStream.fromFlow` creates a stream containing the successful result of one Flow:

```fsharp transcript
> (Flow.succeed "Ada" : Flow<string>)
- |> FlowStream.fromFlow
- |> FlowStream.runCollect
- |> Flow.run ();;
val it: Exit<string list,Never> = Success ["Ada"]
```

A failed Flow fails the stream before producing a value.

## Unfold effectful state

`FlowStream.unfoldFlow` repeatedly runs an effectful state transition. Return `Some(value, nextState)` to emit a value
and remember the state for the next pull, or return `None` to finish.

The value and state are separate because they serve different audiences. The value goes downstream; the state stays
inside the source and tells it how to continue. A paginated source might emit a page of messages while retaining an
opaque continuation token. A socket parser might emit a decoded frame while retaining unread bytes. Requiring only a
next state would either emit implementation state to consumers or restrict unfolding to sources whose state happens to
be their output.

```fsharp transcript
> (1
-  |> FlowStream.unfoldFlow (fun number ->
-      Flow.succeed (
-          if number > 3 then None
-          else Some(number, number + 1)))
-  : FlowStream<int>)
- |> FlowStream.runCollect
- |> Flow.run ();;
val it: Exit<int list,Never> = Success [1; 2; 3]
```

Only one step runs per downstream pull. In this small numeric example the emitted value and next state look similar,
but they are independent parts of the transition.

## Exercise one Flow per pull

The distinction is clearer in a paginated source. Here the private state is a page number, while the emitted values are
messages. `Flow.delay` stands in for the client call: it runs only when downstream requests another page.

```fsharp transcript
> let requestedPages = ResizeArray<int>() in
- let fetchPage page =
-     Flow.delay (fun () ->
-         requestedPages.Add page
-         match page with
-         | 1 -> Flow.succeed (Some(["A"; "B"], 2))
-         | 2 -> Flow.succeed (Some(["C"], 3))
-         | _ -> Flow.succeed None)
- in
- (1 |> FlowStream.unfoldFlow fetchPage : FlowStream<string list>)
- |> FlowStream.collect FlowStream.fromSeq
- |> FlowStream.runCollect
- |> Flow.map (fun messages -> List.ofSeq requestedPages, messages)
- |> Flow.run ();;
val requestedPages: List<int> = seq [1; 2; 3]
val it: Exit<Tuple<int list,string list>,Never> = Success ([1; 2; 3], ["A"; "B"; "C"])
```

The terminal pull of page 3 returns `None`, so it emits no value. The result shows both sides of the source: three
effectful page requests occurred, while consumers saw only the three messages. Because pulls are demand-driven,
placing `FlowStream.take 2` before the terminal consumer would stop after enough messages and prevent unnecessary later
pulls.

This makes `FlowStream.unfoldFlow` the basic integration point for paginated APIs, sockets, subscriptions, and other
host adapters without moving their I/O into Axial core.
