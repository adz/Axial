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

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
flow {
    let! loadConfig = Flow.memoize readConfigFromDisk
    let! a = loadConfig
    let! b = loadConfig // the same value; the file is read once
    return a = b
}
```

## Cache by key

`Cache.make` takes a lookup function and returns a cache. `Cache.get` returns the value for a key, running the lookup
only when no success is cached and no lookup for that key is already running:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
flow {
    let! users = Cache.make loadUser
    let! pages = userIds |> Flow.traversePar (Parallelism.bounded 8) (fun id -> users |> Cache.get id)
    return pages
}
```

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
