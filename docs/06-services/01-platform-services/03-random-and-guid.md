---
title: Randomness and GUIDs
description: Non-deterministic values as declared dependencies you can pin in a test.
---

`IRandom` and `IGuid` exist for the same reason as the clock: a workflow that calls `Guid.NewGuid()` directly cannot
be asserted against, and one that declares the dependency can.

```fsharp
open System
open Axial
open Axial.PlatformService
```

## Randomness

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
Random.next                       // non-negative int
Random.nextMax exclusiveMax       // 0 <= value < exclusiveMax
Random.nextInt minimum maximum    // minimum <= value < maximum
Random.nextDouble                 // 0.0 <= value < 1.0
Random.nextBytes buffer           // fills an existing buffer
Random.bytes count                // allocates and fills a new array
```

`Random.bytes` is the one to prefer when you want a fresh array — it allocates the buffer, fills it, and returns it
in one step.

`Random.live` is backed by the platform generator. Two doubles cover most tests: `Random.fromValue` returns the same
integer from every method, and `Random.fromFixed integer double byte` pins the three value kinds separately when a
test cares about the difference.

None of these are cryptographic. For key material or tokens, use a cryptographic generator behind your own service
contract rather than `IRandom`.

## GUIDs

`IGuid` has one member. `Guid.newGuid` reads it:

```fsharp
let tagged name : Flow<#IHasGuid, Never, string> =
    Guid.newGuid |> Flow.map (fun id -> $"{name}-{id}")
```

`Guid.live` calls `System.Guid.NewGuid()`. `Guid.fromValue` returns a fixed identifier, which makes generated
identifiers assertable:

```fsharp
let guid = Guid.fromValue (System.Guid.Parse "11111111-1111-1111-1111-111111111111")
```

A fixed `IGuid` returns the *same* value every call. When a test needs distinct-but-predictable identifiers,
implement `IGuid` over a counter — the point is that the sequence lives in the test, not in the workflow.

Both services are part of [the base runtime](index.html), so applications usually receive them as one bundle rather
than wiring each.
