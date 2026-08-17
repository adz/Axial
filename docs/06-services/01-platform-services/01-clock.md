---
title: Clock
description: Read the current instant through an explicit service.
---

`IClock` has one member, `UtcNow()`, and always reports UTC. Reading it through the service is what lets a test
choose the instant:

```fsharp
open System
open Axial
open Axial.PlatformService
```

```fsharp
let expiresWithin (window: TimeSpan) (expiry: DateTimeOffset) : Flow<#IHasClock, Never, bool> =
    Clock.now |> Flow.map (fun now -> expiry - now <= window)
```

`Clock.now` returns a `DateTimeOffset`. The other readers derive from it:

```fsharp
Clock.now                    // DateTimeOffset
Clock.utcDateTime            // DateTime, Kind = Utc
Clock.unixTimeSeconds        // int64
Clock.unixTimeMilliseconds   // int64
```

None of them produce a typed failure, so the error channel stays free for the workflow's own errors.

## Supplying the service

`Clock.live` reads `DateTimeOffset.UtcNow`. `Clock.layer` is the same value as a `Layer<unit, Never, IClock>`. Most
applications get the clock as part of [the base runtime](index.html) rather than wiring it alone.

## Testing

`Clock.fromValue` pins the instant, which turns a time-dependent assertion into an ordinary one:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let clock = Clock.fromValue (DateTimeOffset.Parse "2026-01-01T12:00:00Z")
let exit = expiresWithin (TimeSpan.FromHours 1.0) deadline |> Flow.run { Clock = clock }
```

A clock that returns a fixed instant does not advance, which is usually what you want for assertions. When a test
needs time to move, supply an `IClock` closing over a mutable field and step it explicitly — that keeps the
progression in the test rather than in wall-clock timing.

Note that `IClock` reports the time; it does not schedule. Delays, timeouts, and retry policies are Flow runtime
concerns — see [scheduling and retries](/scheduling-and-retries/index.html).
