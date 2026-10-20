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
// A simulated config load: increments a counter instead of touching the filesystem, so the example
// demonstrates memoization without real I/O.
let simulateConfigLoad (reads: Ref<int>) : Flow<ClockEnvironment, string, string> =
    reads |> Ref.updateAndGet ((+) 1) |> Flow.map (fun count -> $"config read {count} time(s)")

/// Loads the config twice through one memoized flow, and returns whether both loads agree and how often it was read.
let sameConfig : Flow<ClockEnvironment, string, bool * int> =
    flow {
        let! reads = Ref.make 0
        let! loadConfig = Flow.memoize (simulateConfigLoad reads)
        let! a = loadConfig
        let! b = loadConfig // the same value; the load runs once
        let! readCount = Ref.get reads
        return a = b, readCount
    }
```

```fsharp run
sameConfig |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success(true, 1))
```

## Cache by key

`Cache.make` requires `'env :> IHasClock`: the runtime uses the supplied clock to schedule detached lookup fibers. `Cache.makeBounded capacity` evicts the least recently read key when full; `Cache.makeBoundedWithExpiry capacity timeToLive` also refreshes successful entries after the monotonic clock's elapsed duration. Eviction does not cancel existing waiters. `Cache.makeRec` passes the cache to its lookup for nested reads. Do not look up the same key recursively, directly or through a cycle: single-flight waits for the running lookup and deadlocks.

`Cache.make` takes a lookup function and returns a cache. `Cache.get` returns the value for a key, running the lookup
only when no success is cached and no lookup for that key is already running:

```fsharp
let loadUser (lookups: Ref<int>) (id: int) : Flow<ClockEnvironment, string, string> =
    flow {
        do! Flow.sleep (TimeSpan.FromMilliseconds 5.0)
        do! lookups |> Ref.update ((+) 1)
        return $"user {id}"
    }

/// Loads a page per user id through one cache, and returns the pages and how many lookups ran.
let loadPages (userIds: int list) : Flow<ClockEnvironment, string, string list * int> =
    flow {
        let! lookups = Ref.make 0
        let! users = Cache.make (loadUser lookups)
        let! pages = userIds |> Flow.traversePar (Parallelism.bounded 8) (fun id -> users |> Cache.get id)
        let! lookupCount = Ref.get lookups
        return pages, lookupCount
    }
```

```fsharp run
loadPages [ 1; 2; 1; 1; 2 ]
|> Flow.run (ClockEnvironment Clock.live)
|> shouldEqual (Exit.Success([ "user 1"; "user 2"; "user 1"; "user 1"; "user 2" ], 2))
```

Five concurrent requests for two users ran the lookup twice.

`Cache.invalidate` forgets one key and `Cache.invalidateAll` forgets every key, so the next `get` looks up again.
`Cache.count` reports how many keys are cached or loading.

## Expiry, eviction, and in-flight loads

`Cache.makeBoundedWithExpiry` expires a successful entry after the monotonic clock's time-to-live. The cache reads
the environment's clock, so a manual clock makes expiry deterministic:

```fsharp
let manualClock = ManualClock(DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))

let expiryLoads : Flow<ClockEnvironment, string, string list * int> =
    flow {
        let! lookups = Ref.make 0

        let loadUser (id: int) : Flow<ClockEnvironment, string, string> =
            flow {
                do! lookups |> Ref.update ((+) 1)
                return $"user {id}"
            }

        let! users = Cache.makeBoundedWithExpiry 2 (TimeSpan.FromSeconds 10.0) loadUser
        let! first = users |> Cache.get 1
        let! second = users |> Cache.get 1
        manualClock.AdvanceBy(TimeSpan.FromSeconds 11.0)
        let! third = users |> Cache.get 1
        let! count = Ref.get lookups
        return [ first; second; third ], count
    }
```

```fsharp run
expiryLoads |> Flow.run (ClockEnvironment manualClock) |> shouldEqual (Exit.Success([ "user 1"; "user 1"; "user 1" ], 2))
```

The first two reads shared one lookup; after the clock passed the time-to-live, the third read started a second
lookup. Bounded eviction removes the least recently read key once the capacity is exceeded.

Invalidation and eviction never cancel an in-flight lookup or its existing waiters: those waiters still receive the
result, but the key is no longer cached, so the next `get` starts a fresh lookup:

```fsharp
let invalidateInFlight : Flow<ClockEnvironment, string, string * string * int> =
    flow {
        let! lookups = Ref.make 0
        let! loadStarted = Deferred.make<ClockEnvironment, string, unit> ()
        let! finishLoad = Deferred.make<ClockEnvironment, string, unit> ()

        let load (id: int) : Flow<ClockEnvironment, string, string> =
            flow {
                do! lookups |> Ref.update ((+) 1)
                do! Deferred.succeed () loadStarted |> Flow.ignore
                do! Deferred.await finishLoad
                return $"user {id}"
            }

        let! users = Cache.make load
        let! waiter = users |> Cache.get 1 |> Flow.fork
        do! Deferred.await loadStarted
        do! users |> Cache.invalidate 1
        do! Deferred.succeed () finishLoad |> Flow.ignore
        let! first = Fiber.join waiter
        let! second = users |> Cache.get 1
        let! count = Ref.get lookups
        return first, second, count
    }
```

```fsharp run
invalidateInFlight |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success("user 1", "user 1", 2))
```

## Rules

- **Successes are kept; failures are not.** A failed lookup is removed when it settles, so the next caller starts a
  fresh attempt instead of receiving a cached error.
- **Interrupting a caller stops only its wait.** Lookups run as detached fibers in the scope and environment where
  the cache (or memoized flow) was made, not in any caller's. A caller that is interrupted, for example because a user
  navigated away, never cancels a lookup that other callers are still waiting for.
- **The owner's scope bounds the lookups.** Closing the scope where the cache was made interrupts any lookup still
  running. Make the cache where it should live, typically in a service's layer.
- **Values are kept until invalidated, evicted, or expired.** `Cache.make` and `Flow.memoize` never expire an entry,
  so invalidate keys when their source changes. A bounded cache also evicts the least recently read key when full, and
  `Cache.makeBoundedWithExpiry` expires successful entries after its time-to-live.

The [cache torture test](torture-tests/caches.html) interrupts callers, fails lookups, and invalidates keys while
hundreds of callers share one cache, and checks each rule above.
