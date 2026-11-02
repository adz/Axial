---
title: Culture and time zones
description: Read machine culture and time zone values explicitly, and preserve caller-supplied TimeZoneInfo rules.
---

# Culture and time zones

`Culture` and `TimeZone` are explicit values. Read the machine defaults through a flow when a value should be
recorded in the environment or test platform:

```fsharp
open Axial

let localZone () : Flow<'env, 'error, Axial.TimeZone> = TimeZone.local
let localCulture () : Flow<'env, 'error, Culture> = Culture.system
```

`TimeZone.local` is an ambient machine read only inside the flow. It does not make `System.TimeZoneInfo.Local` part
of a pure function.

## Convert to and from `System.TimeZoneInfo`

On .NET, an `Axial.TimeZone` preserves the actual `System.TimeZoneInfo` it was created from. That keeps custom
adjustment rules that are not available by id in the operating-system database:

```fsharp
open System
open Axial

let custom =
    TimeZoneInfo.CreateCustomTimeZone(
        "Application/Shift",
        TimeSpan.FromHours -5.0,
        "Application shift",
        "Application standard",
        "Application daylight",
        [| TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
               DateTime(2020, 1, 1),
               DateTime(2030, 12, 31),
               TimeSpan.FromHours 1.0,
               TimeZoneInfo.TransitionTime.CreateFloatingDateRule(DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday),
               TimeZoneInfo.TransitionTime.CreateFloatingDateRule(DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday)) |]
    )

let zone = Axial.TimeZone.ofTimeZoneInfo custom
let preserved = Axial.TimeZone.toTimeZoneInfo zone
let wallClock = Axial.TimeZone.toLocal zone (DateTimeOffset(2024, 7, 1, 12, 0, 0, TimeSpan.Zero))
```

`ofTimeZoneInfo` rejects a null argument with `ArgumentNullException`. `toTimeZoneInfo` returns the preserved object;
it does not look the id up again. Neither function reads the machine-local zone. Caller-supplied custom zones therefore
keep their rules even when the id is absent from the OS database.

On Fable, `Axial.TimeZone` carries only the id and these .NET-only conversions are not exported. Use
`TimeZone.ofId` and `TimeZone.toLocal`.
