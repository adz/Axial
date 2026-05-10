---
title: "CAPS Core"
---

`FsFlow.Caps.Core` is the smallest shared capability package in the FsFlow CAPS story. It keeps the surface synchronous and explicit: clock, random, GUID, and environment-variable capabilities.

## Capability types

- [`FsFlow.Caps.Core.IClock`](./t-core-iclock.md): Provides synchronous access to the current UTC clock.
- [`FsFlow.Caps.Core.IRandom`](./t-core-irandom.md): Provides synchronous random-number generation.
- [`FsFlow.Caps.Core.IGuid`](./t-core-iguid.md): Provides synchronous GUID generation.
- [`FsFlow.Caps.Core.IEnvironmentVariables`](./t-core-ienvironmentvariables.md): Provides synchronous environment-variable lookup.
- [`FsFlow.Caps.Core.EnvironmentVariableError`](./t-core-environmentvariableerror.md): Describes a meaningful environment-variable failure.

## Clock

- [`FsFlow.Caps.Core.Clock.now`](./m-core-clock-now.md): Reads the current UTC timestamp from the environment.
- [`FsFlow.Caps.Core.Clock.live`](./p-core-clock-live.md): Creates a live clock backed by `UtcNow`.
- [`FsFlow.Caps.Core.Clock.fromValue`](./m-core-clock-fromvalue.md): Creates a deterministic clock that always returns the supplied instant.

## Random

- [`FsFlow.Caps.Core.Random.nextInt`](./m-core-random-nextint.md): Reads a random integer from the environment.
- [`FsFlow.Caps.Core.Random.live`](./p-core-random-live.md): Creates a live random-number generator backed by `Random`.
- [`FsFlow.Caps.Core.Random.fromValue`](./m-core-random-fromvalue.md): Creates a deterministic random generator that always returns the supplied value.

## GUID

- [`FsFlow.Caps.Core.Guid.newGuid`](./m-core-guid-newguid.md): Reads a GUID from the environment.
- [`FsFlow.Caps.Core.Guid.live`](./p-core-guid-live.md): Creates a live GUID generator backed by `NewGuid`.
- [`FsFlow.Caps.Core.Guid.fromValue`](./m-core-guid-fromvalue.md): Creates a deterministic GUID generator that always returns the supplied value.

## Environment variables

- [`FsFlow.Caps.Core.EnvironmentVariables.tryGet`](./m-core-environmentvariables-tryget.md): Reads a raw environment-variable value from the environment.
- [`FsFlow.Caps.Core.EnvironmentVariables.live`](./p-core-environmentvariables-live.md): Creates a live provider backed by the current process environment.
- [`FsFlow.Caps.Core.EnvironmentVariables.fromPairs`](./m-core-environmentvariables-frompairs.md): Creates a deterministic provider from a fixed set of name/value pairs.
- [`FsFlow.Caps.Core.EnvironmentVariable.tryGet`](./m-core-environmentvariable-tryget.md): Reads a raw string environment variable without wrapping it in a result.
- [`FsFlow.Caps.Core.EnvironmentVariable.get`](./m-core-environmentvariable-get.md): Reads a raw string environment variable from the environment.
- [`FsFlow.Caps.Core.EnvironmentVariable.getInt`](./m-core-environmentvariable-getint.md): Reads an integer environment variable from the environment.
- [`FsFlow.Caps.Core.EnvironmentVariable.getGuid`](./m-core-environmentvariable-getguid.md): Reads a GUID environment variable from the environment.
- [`FsFlow.Caps.Core.EnvironmentVariable.getBool`](./m-core-environmentvariable-getbool.md): Reads a boolean environment variable from the environment.
- [`FsFlow.Caps.Core.EnvironmentVariableErrors.describe`](./m-core-environmentvariableerrors-describe.md): Formats a human-readable description for an error.

