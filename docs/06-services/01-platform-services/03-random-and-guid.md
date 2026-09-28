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

| Function | Result |
| --- | --- |
| `Random.next` | A non-negative `int` |
| `Random.nextMax exclusiveMax` | `0 <= value < exclusiveMax` |
| `Random.nextInt minimum maximum` | `minimum <= value < maximum` |
| `Random.nextDouble` | `0.0 <= value < 1.0` |
| `Random.nextBytes buffer` | Fills an existing buffer |
| `Random.bytes count` | Allocates and fills a new array |

With the base runtime's generator replaced by a fixed one, every call is predictable:

```fsharp transcript
> open Axial.PlatformService;;
> let runtime () = { BaseRuntime.liveValue with Random = Random.fromFixed 7 0.25 9uy };;
> (Random.nextInt 1 10 : Flow<BaseRuntime, Never, int>) |> Flow.run (runtime ());;
val it: Exit<int,Never> = Success 7

> (Random.nextDouble : Flow<BaseRuntime, Never, float>) |> Flow.run (runtime ());;
val it: Exit<float,Never> = Success 0.25

> (Random.bytes 3 : Flow<BaseRuntime, Never, byte array>) |> Flow.run (runtime ());;
val it: Exit<Byte[],Never> = Success [|9uy; 9uy; 9uy|]
```

Use `Random.bytes` for a fresh array: it allocates the buffer, fills it, and returns it in one step.

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

```fsharp transcript
> open System;;
> open Axial.PlatformService;;
> let runtime () = { BaseRuntime.liveValue with Guid = Guid.fromValue (Guid.Parse "11111111-1111-1111-1111-111111111111") };;
> let tagged name : Flow<BaseRuntime, Never, string> = Guid.newGuid |> Flow.map (fun id -> $"{name}-{id}");;
> tagged "order" |> Flow.run (runtime ());;
val it: Exit<string,Never> = Success "order-11111111-1111-1111-1111-111111111111"
```

A fixed `IGuid` returns the *same* value every call. When a test needs distinct-but-predictable identifiers,
implement `IGuid` over a counter, so the sequence is defined in the test, not in the workflow.

Both services are part of [the base runtime](index.html), so applications usually receive them as one bundle rather
than wiring each.
