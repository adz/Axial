---
title: Resources and Results
---

# Resources and Results

## Own a resource for the life of a stream

A stream over a file, a socket, or a database cursor has to close it when consumption ends, however it ends.
`FlowStream.using` acquires a `Resource` on the first pull and releases it when the consuming Flow finishes: when the
stream is exhausted, when it fails, when it is interrupted, or when `take` stops it early.

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

```fsharp
let readersClosed = ref 0

let openReader (path: string) : Resource<unit, Never, StreamReader> =
    Resource.create
        (Flow.delay (fun () -> Flow.ok (File.OpenText path)))
        (fun reader _ ->
            reader.Dispose()
            readersClosed.Value <- readersClosed.Value + 1
            Task.CompletedTask)

let lines (path: string) : FlowStream<string> =
    FlowStream.using (openReader path) (fun reader ->
        FlowStream.repeatFlow (Flow.fromBlocking (fun _ -> reader.ReadLine()))
        |> FlowStream.takeWhile (isNull >> not))
```

```fsharp run
let path = Path.Combine(Path.GetTempPath(), $"axial-docs-lines-{Guid.NewGuid():N}.txt")
File.WriteAllLines(path, [ "first"; "second"; "third" ])

lines path |> FlowStream.runCollect |> Flow.run () |> shouldEqual (Exit.Success [ "first"; "second"; "third" ])
lines path |> FlowStream.take 1 |> FlowStream.runCollect |> Flow.run () |> shouldEqual (Exit.Success [ "first" ])
readersClosed.Value |> shouldEqual 2
File.Delete path
```

The reader was closed after the full read and after `take 1` stopped the stream early.

`FlowStream.repeatFlow` runs its flow once per pull, forever, so it never runs ahead of the consumer. Bound it with
`take` or `takeWhile`, or pace it with `throttle`.

## Take one result

Some consumers only need part of a stream:

- `FlowStream.runTryHead` pulls a single value and returns it, or `None` for an empty stream. It stops the stream as
  soon as the value arrives, so resources and producer fibers are released right away.
- `FlowStream.runTryLast` consumes the whole stream and returns its last value, or `None`.
- `FlowStream.runCount` consumes the whole stream and returns how many values it emitted.

```fsharp
let firstLongLine (path: string) : Flow<string option> =
    lines path |> FlowStream.filter (fun line -> line.Length > 5) |> FlowStream.runTryHead
```
