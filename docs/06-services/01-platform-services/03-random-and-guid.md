---
title: Randomness and GUIDs
description: Runtime values controlled by scoped fixed, seeded and scripted implementations.
---

`Random.*` and `Guid.newGuid` read runtime services, not fields in `'env`. Use `Flow.withRandom` and
`Flow.withGuid` to override them for a subtree, including forked fibers. Leaving that subtree restores the outer
services. `IRandom` and `IGuid` are defined in `Axial`; their modules live in `Axial.PlatformService`.

## Randomness

| Function | Result |
| --- | --- |
| `Random.next` | A non-negative `int` |
| `Random.nextMax exclusiveMax` | `0 <= value < exclusiveMax` |
| `Random.nextInt minimum maximum` | `minimum <= value < maximum` |
| `Random.nextDouble` | `0.0 <= value < 1.0` |
| `Random.nextBytes buffer` | Fills an existing buffer |
| `Random.bytes count` | Allocates and fills a fresh array |

Pin each value kind independently in a test:

```fsharp transcript
> open Axial;;
> open Axial.PlatformService;;
> let fixedRandom () = Random.fromFixed 7 0.25 9uy;;
> (Random.nextInt 1 10 : Flow<unit, Never, int>) |> Flow.withRandom (fixedRandom ()) |> Flow.run ();;
val it: Exit<int,Never> = Success 7

> (Random.nextDouble : Flow<unit, Never, float>) |> Flow.withRandom (fixedRandom ()) |> Flow.run ();;
val it: Exit<float,Never> = Success 0.25

> (Random.bytes 3 : Flow<unit, Never, byte array>) |> Flow.withRandom (fixedRandom ()) |> Flow.run ();;
val it: Exit<Byte[],Never> = Success [|9uy; 9uy; 9uy|]
```

`Random.live` is the runtime default. `Random.seeded seed` creates a repeatable generator; construct it once per
test rather than sharing mutable generator state across tests. `Random.fromValue integer` pins integer reads;
`Random.fromFixed integer double byte` pins all three kinds. Scripted implementations can deliberately return
values outside normal generator bounds, so choose values appropriate to the operation under test.

None of these generators are cryptographic. For secrets, tokens and nonces, use `System.Security.Cryptography`
inside an explicit capability, not the runtime's ordinary random service.

## GUIDs

```fsharp transcript
> open System;;
> open Axial;;
> open Axial.PlatformService;;
> let tagged name : Flow<unit, Never, string> = Guid.newGuid |> Flow.map (fun id -> $"{name}-{id}");;
> tagged "order" |> Flow.withGuid (Guid.fromValue (System.Guid.Parse "11111111-1111-1111-1111-111111111111")) |> Flow.run ();;
val it: Exit<string,Never> = Success "order-11111111-1111-1111-1111-111111111111"
```

`Guid.live` is the default. `Guid.fromValue` returns the same identifier on every call. `Guid.fromSequence` supplies
a finite scripted sequence and fails clearly on exhaustion. Use a fresh generator per test.

`Flow.runTest` creates seeded randomness and an infinite sequential GUID generator for each run, alongside its
manual clock and recording log. For custom sequences, set `TestPlatform.Random` / `Guid` before
`Flow.runTestWith platform env`, or use the scoped overrides above. A library function can use these readers under
any application environment without adding platform-service interfaces to its signature.
