---
title: "Capabilities Core"
---

This page shows the core runtime capability package: clock, logging, random numbers, GUID generation, and environment-variable lookup. These services are operational concerns, so ordinary application workflows should usually read them through helpers such as `Clock.now`, `Log.info`, `Random.nextInt`, `Guid.newGuid`, and `EnvironmentVariable.get` instead of adding them to every app environment record. Tests and scoped workflows can replace them with deterministic providers via `Flow.withClock`, `Flow.withLog`, `Flow.withRandom`, `Flow.withGuid`, and `Flow.withEnvironmentVariables`.

## Capability types

- [`IClock`](./t-capabilities-core-iclock.md): Provides synchronous access to the current UTC clock.
- [`IRandom`](./t-capabilities-core-irandom.md): Provides synchronous random-number generation.
- [`IGuid`](./t-capabilities-core-iguid.md): Provides synchronous GUID generation.
- [`IEnvironmentVariables`](./t-capabilities-core-ienvironmentvariables.md): Provides synchronous environment-variable lookup.
- [`Capabilities.Core.EnvironmentVariableError`](./t-capabilities-core-environmentvariableerror.md): Describes a meaningful environment-variable failure.

## Clock

- [`Capabilities.Core.Clock.now`](./m-capabilities-core-clock-now.md): Reads the current UTC timestamp from the ambient runtime.
- [`Capabilities.Core.Clock.live`](./m-capabilities-core-clock-live.md): Creates a live clock backed by <a href="https://learn.microsoft.com/dotnet/api/system.datetimeoffset.utcnow">DateTimeOffset.UtcNow</a>.
- [`Capabilities.Core.Clock.fromValue`](./m-capabilities-core-clock-fromvalue.md): Creates a deterministic clock that always returns the supplied instant.

## Random

- [`Capabilities.Core.Random.nextInt`](./m-capabilities-core-random-nextint.md): Reads a random integer from the ambient runtime.
- [`Capabilities.Core.Random.live`](./m-capabilities-core-random-live.md): Creates a live random-number generator backed by <a href="https://learn.microsoft.com/dotnet/api/system.random">Random</a>.
- [`Capabilities.Core.Random.fromValue`](./m-capabilities-core-random-fromvalue.md): Creates a deterministic random generator that always returns the supplied value.

## GUID

- [`Capabilities.Core.Guid.newGuid`](./m-capabilities-core-guid-newguid.md): Reads a GUID from the ambient runtime.
- [`Capabilities.Core.Guid.live`](./m-capabilities-core-guid-live.md): Creates a live GUID generator backed by <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-capabilities-core-guid.html">Guid.NewGuid</a>.
- [`Capabilities.Core.Guid.fromValue`](./m-capabilities-core-guid-fromvalue.md): Creates a deterministic GUID generator that always returns the supplied value.

## Environment variables

- [`Capabilities.Core.EnvironmentVariables.tryGet`](./m-capabilities-core-environmentvariables-tryget.md): Reads a raw environment-variable value from the ambient runtime.
- [`Capabilities.Core.EnvironmentVariables.live`](./m-capabilities-core-environmentvariables-live.md): Creates a live provider backed by the current process environment.
- [`Capabilities.Core.EnvironmentVariables.fromPairs`](./m-capabilities-core-environmentvariables-frompairs.md): Creates a deterministic provider from a fixed set of name/value pairs.
- [`Capabilities.Core.EnvironmentVariable.tryGet`](./m-capabilities-core-environmentvariable-tryget.md): Reads a raw string environment variable without wrapping it in a result.
- [`Capabilities.Core.EnvironmentVariable.get`](./m-capabilities-core-environmentvariable-get.md): Reads a raw string environment variable from the ambient runtime.
- [`Capabilities.Core.EnvironmentVariable.getInt`](./m-capabilities-core-environmentvariable-getint.md): Reads an integer environment variable from the ambient runtime.
- [`Capabilities.Core.EnvironmentVariable.getGuid`](./m-capabilities-core-environmentvariable-getguid.md): Reads a GUID environment variable from the ambient runtime.
- [`Capabilities.Core.EnvironmentVariable.getBool`](./m-capabilities-core-environmentvariable-getbool.md): Reads a boolean environment variable from the ambient runtime.
- [`Capabilities.Core.EnvironmentVariableErrors.describe`](./m-capabilities-core-environmentvariableerrors-describe.md): Formats a human-readable description for an error.

