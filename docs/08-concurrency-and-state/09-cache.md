---
title: Cache and Memoize
description: Share one computation between concurrent callers, keep successes, and retry failures.
---

# Cache and Memoize

When several callers ask for the same expensive value at once, each one starting its own load wastes work and can
overload the source. `Cache` and `Flow.memoize` run the load once and let every caller wait for that single result.
This is often called single-flight.

## Memoize one computation

`Flow.memoize` turns a flow into a shared version of itself:

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
let reads = ref 0

let readConfigFromDisk : Flow<ClockEnvironment, string, string> =
    Flow.delay (fun () -> Flow.ok $"config read {Interlocked.Increment &reads.contents} time(s)")

let sameConfig : Flow<ClockEnvironment, string, bool> =
    flow {
        let! loadConfig = Flow.memoize readConfigFromDisk
        let! a = loadConfig
        let! b = loadConfig // the same value; the file is read once
        return a = b
    }
```

```fsharp run
sameConfig |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success true)
reads.Value |> shouldEqual 1
```

## Cache by key

`Cache.make` takes a lookup function and returns a cache. `Cache.get` returns the value for a key, running the lookup
only when no success is cached and no lookup for that key is already running:

```fsharp
let lookups = ref 0

let loadUser (id: int) : Flow<ClockEnvironment, string, string> =
    Flow.sleep (TimeSpan.FromMilliseconds 5.0)
    |> Flow.map (fun () ->
        Interlocked.Increment &lookups.contents |> ignore
        $"user {id}")

let loadPages (userIds: int list) : Flow<ClockEnvironment, string, string list> =
    flow {
        let! users = Cache.make loadUser
        let! pages = userIds |> Flow.traversePar (Parallelism.bounded 8) (fun id -> users |> Cache.get id)
        return pages
    }
```

```fsharp run
loadPages [ 1; 2; 1; 1; 2 ] |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success [ "user 1"; "user 2"; "user 1"; "user 1"; "user 2" ])
lookups.Value |> shouldEqual 2
```

Five concurrent requests for two users ran the lookup twice.

`Cache.invalidate` forgets one key and `Cache.invalidateAll` forgets every key, so the next `get` looks up again.
`Cache.count` reports how many keys are cached or loading.

## Rules

- **Successes are kept; failures are not.** A failed lookup is removed when it settles, so the next caller starts a
  fresh attempt instead of receiving a cached error.
- **Interrupting a caller stops only its wait.** Lookups run as detached fibers in the scope and environment where
  the cache (or memoized flow) was made, not in any caller's. A caller that is interrupted, for example because a user
  navigated away, never cancels a lookup that other callers are still waiting for.
- **The owner's scope bounds the lookups.** Closing the scope where the cache was made interrupts any lookup still
  running. Make the cache where it should live, typically in a service's layer.
- **Values are kept until invalidated.** There is no expiry; invalidate keys when their source changes.

The [cache torture test](torture-tests/caches.html) interrupts callers, fails lookups, and invalidates keys while
hundreds of callers share one cache, and checks each rule above.
