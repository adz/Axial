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

## Fetch a known list concurrently

Suppose `fetchHtml` performs one HTTP request as a Flow, while `extractLinks` is a total function from an HTML document
to the links it contains. When the URLs are already known, construct a stream from the list and overlap requests with
`FlowStream.mapFlowPar`:

```fsharp no-check reason="fetchHtml and HtmlPage are application HTTP abstractions described in the surrounding prose"
let fetchKnownPages urls =
    urls
    |> FlowStream.fromSeq
    |> FlowStream.mapFlowPar
        (Parallelism.bounded 4)
        (fun url ->
            fetchHtml url
            |> Flow.map (fun html ->
                { Url = url
                  Html = html }))
```

This keeps at most four requests active or waiting to be emitted. Pages arrive in completion order. Stopping downstream
also interrupts and awaits requests that are still running.

## Discover and pull linked pages

A crawler does not know every URL up front. Each response discovers more work. Here the unfold state is the private
crawl frontier—pending URLs plus the URLs already seen—while each emitted value is a fetched page:

```fsharp no-check reason="fetchHtml, extractLinks, and HtmlPage are application HTTP abstractions described in the surrounding prose"
type CrawlState =
    { Pending: string list
      Seen: Set<string> }

let crawl seeds =
    { Pending = seeds
      Seen = Set.empty }
    |> FlowStream.unfoldFlow (fun state ->
        let batch =
            state.Pending
            |> List.filter (fun url -> not (Set.contains url state.Seen))
            |> List.distinct
            |> List.truncate 4

        if List.isEmpty batch then
            Flow.succeed None
        else
            let seen = Set.union state.Seen (Set.ofList batch)
            let remaining = List.except batch state.Pending

            batch
            |> List.map (fun url ->
                fetchHtml url
                |> Flow.map (fun html ->
                    { Url = url
                      Html = html }))
            |> Flow.sequencePar
            |> Flow.map (fun pages ->
                let discovered =
                    pages
                    |> List.collect (fun page -> extractLinks page.Html)
                    |> List.filter (fun url -> not (Set.contains url seen))

                Some(
                    pages,
                    { Pending = remaining @ discovered
                      Seen = seen })))
    |> FlowStream.collect FlowStream.fromSeq
```

One downstream pull fetches one frontier batch. `Flow.sequencePar` fetches the batch concurrently and preserves its URL
order. The total `extractLinks` function turns those pages into the next private frontier. `FlowStream.collect` then
flattens each emitted page batch so consumers see `HtmlPage` values rather than crawler state.

Backpressure applies between frontier batches: the crawler does not fetch the next discovered batch until downstream
pulls again. A terminal consumer supplies the stream scope, so failure, interruption, or early termination cancels
outstanding requests and closes their resources. This pattern also applies to paginated APIs, where the private state
is a continuation token and each emitted value is a page or item.
