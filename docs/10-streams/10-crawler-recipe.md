---
title: Crawler recipe
linkTitle: Crawler recipe
description: Build a concurrent link crawler from FlowStream.unfoldFlow, mapFlowPar, and sequencePar.
---

# Crawler recipe

This recipe combines the construction primitives from [Constructing streams](constructing.html) into a concurrent
crawler: `FlowStream.unfoldFlow` holds the private frontier, `FlowStream.mapFlowPar` overlaps fetches, and
`FlowStream.collect` flattens each page batch. It also applies to paginated APIs, where the private state is a
continuation token.

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Layers
open Axial.Console
open Axial.FileSystem
open Axial.Hosting
open Axial.Hosting.Browser
open Axial.Hosting.Node
open Axial.PlatformService
open Axial.State
open Axial.Telemetry
open Axial.Telemetry.JavaScript

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

## Fetch a known list concurrently

Suppose `fetchHtml` performs one HTTP request as a Flow, while `extractLinks` is a total function from an HTML document
to the links it contains. When the URLs are already known, construct a stream from the list and overlap requests with
`FlowStream.mapFlowPar`:

The examples use a small in-memory site in place of HTTP; each page lists the pages it links to:

```fsharp
type HtmlPage = { Url: string; Html: string }

let site =
    Map [ "/", "links: /docs /blog"
          "/docs", "links: /docs/flow /"
          "/blog", "links: /"
          "/docs/flow", "links:" ]

let fetchHtml (url: string) : Flow<ClockEnvironment, Never, string> =
    Flow.sleep (TimeSpan.FromMilliseconds 1.0) |> Flow.map (fun () -> Map.find url site)

let extractLinks (html: string) : string list =
    html.Split(' ', StringSplitOptions.RemoveEmptyEntries) |> Array.skip 1 |> List.ofArray
```

`fetchHtml` looks each URL up with `Map.find url site`, which throws for a URL absent from `site`. The example
site lists only pages that exist, so every discovered link resolves; a real crawl over an open set needs a typed
fetch error or an explicit missing-page policy instead of an exception from `Map.find`.

```fsharp
let fetchKnownPages (urls: string list) : FlowStream<ClockEnvironment, Never, HtmlPage> =
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

`mapFlowPar` emits pages in completion order, so sort before comparing:

```fsharp run
fetchKnownPages [ "/"; "/docs"; "/blog" ]
|> FlowStream.map _.Url
|> FlowStream.runCollect
|> Flow.map List.sort
|> Flow.run (ClockEnvironment Clock.live)
|> shouldEqual (Exit.Success [ "/"; "/blog"; "/docs" ])
```

This keeps at most four requests active or waiting to be emitted. Pages arrive in completion order. Stopping downstream
also interrupts and awaits requests that are still running.

## Discover and pull linked pages

A crawler does not know every URL up front. Each response discovers more work. Here the unfold state is the private
crawl frontier (pending URLs plus the URLs already seen), while each emitted value is a fetched page:

```fsharp
type CrawlState =
    { Pending: string list
      Seen: Set<string> }

let fetchPagesAndDiscoverLinks (seen: Set<string>) (urls: string list) : Flow<ClockEnvironment, Never, HtmlPage list * string list> =
    urls
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

        pages, discovered)

let crawl (seeds: string list) : FlowStream<ClockEnvironment, Never, HtmlPage> =
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

            flow {
                let! pages, discovered =
                    fetchPagesAndDiscoverLinks seen batch

                return
                    Some(
                        pages,
                        { Pending = remaining @ discovered
                          Seen = seen })
            })
    |> FlowStream.collect FlowStream.fromSeq
```

Starting from the home page, the crawl finds every page once, a frontier batch at a time:

```fsharp run
crawl [ "/" ] |> FlowStream.map _.Url |> FlowStream.runCollect |> Flow.run (ClockEnvironment Clock.live)
|> shouldEqual (Exit.Success [ "/"; "/docs"; "/blog"; "/docs/flow" ])
```

One downstream pull fetches one frontier batch. `Flow.sequencePar` fetches the batch concurrently and preserves its URL
order. The total `extractLinks` function turns those pages into the next private frontier. `FlowStream.collect` then
flattens each emitted page batch so consumers see `HtmlPage` values rather than crawler state.

Backpressure applies between frontier batches: the crawler does not fetch the next discovered batch until downstream
pulls again. A terminal consumer supplies the stream scope, so failure, interruption, or early termination cancels
outstanding requests and closes their resources.
