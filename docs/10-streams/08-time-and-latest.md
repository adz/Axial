---
title: Time and Latest Value
---

# Time and Latest Value

Most stream operators react only to values. The operators on this page also react to time, or to a newer value
arriving, which is what progress reporting, batching, and search-as-you-type need.

Each of them moves upstream values through a one-slot queue from a producer fiber, so it can wait for "the next value
or a timer" at once. The producer runs in the consuming Flow's scope: it keeps upstream back-pressure, and it stops
when the consumer finishes, fails, or stops early with `take`.

## Batch by size or time

`FlowStream.groupedWithin size window` emits a list when it holds `size` values or when `window` has passed since the
list's first value, whichever comes first. A slow trickle is not held back waiting for a full batch:

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
let batchSizes (events: FlowStream<int>) : Flow<int list> =
    events
    |> FlowStream.groupedWithin 100 (TimeSpan.FromSeconds 1.0)
    |> FlowStream.map List.length
    |> FlowStream.runCollect
```

A burst of 250 events arrives well within a second, so the batches fill by size and the remainder is emitted when the
stream ends:

```fsharp run
batchSizes (FlowStream.fromSeq [ 1..250 ]) |> Flow.run () |> shouldEqual (Exit.Success [ 100; 100; 50 ])
```

No empty list is emitted, and the partial list is emitted when upstream ends or before a failure is propagated.

## Report progress at a steady rate

`FlowStream.throttle interval` emits at most one value per interval. The first value is emitted immediately; values
arriving faster replace each other, and the latest is emitted when the interval ends:

```fsharp
let rendered (progress: FlowStream<int>) : Flow<int list> =
    progress
    |> FlowStream.throttle (TimeSpan.FromMilliseconds 100.0)
    |> FlowStream.runCollect
```

A hundred progress updates in a burst render as the first and the last:

```fsharp run
rendered (FlowStream.fromSeq [ 1..100 ]) |> Flow.run () |> shouldEqual (Exit.Success [ 1; 100 ])
```

## Wait for input to settle

`FlowStream.debounce quiet` emits a value only once `quiet` passes without a newer one, so a burst produces only its
last value. The pending value is emitted when upstream ends.

## Keep only the latest request

`FlowStream.switchMapFlow mapper` runs `mapper` for each value, and interrupts the running flow when a newer value
arrives. Results for stale input are never emitted, and the superseded request's cleanup finishes before the next
one starts:

```fsharp
let search (query: string) : Flow<string> =
    Flow.sleep (TimeSpan.FromMilliseconds 5.0) |> Flow.map (fun () -> $"results for {query}")

let shownResults (keystrokes: FlowStream<string>) : Flow<string list> =
    keystrokes
    |> FlowStream.debounce (TimeSpan.FromMilliseconds 200.0)
    |> FlowStream.switchMapFlow search
    |> FlowStream.runCollect
```

Typing "a", "ax", "axi" quickly searches only once, for the settled input:

```fsharp run
shownResults (FlowStream.fromSeq [ "a"; "ax"; "axi" ]) |> Flow.run () |> shouldEqual (Exit.Success [ "results for axi" ])
```

When upstream ends, the running flow is allowed to finish. The first failure stops the stream.

These operators wait on real timers, so tests that use them depend on timing; give assertions generous margins.
