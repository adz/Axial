---
title: CAPS Core
---

This page shows the source-documented `FsFlow.Caps.Core` surface: the clock, random, GUID, and environment-variable capabilities, plus the live and deterministic providers used by production and tests.

## Capability types

- type [`IClock`](./iclock.md): Provides synchronous access to the current UTC clock. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L8)
- type [`IRandom`](./irandom.md): Provides synchronous random-number generation. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L13)
- type [`IGuid`](./iguid.md): Provides synchronous GUID generation. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L18)
- type [`IEnvironmentVariables`](./ienvironmentvariables.md): Provides synchronous environment-variable lookup. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L23)
- type [`EnvironmentVariableError`](./environmentvariableerror.md): Describes a meaningful environment-variable failure. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L29)
### Constructors

- `MissingVariable of name: string` [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L31)
- `InvalidVariable of name: string * value: string * expected: string` [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L34)


## Clock

- module `Clock`: Helpers for clock capabilities. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L38)
- [`Clock.now`](./clock-now.md): Reads the current UTC timestamp from a clock capability. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L40)
- [`Clock.live`](./clock-live.md): Creates a live clock backed by `UtcNow`. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L43)
- [`Clock.fromValue`](./clock-fromvalue.md): Creates a deterministic clock that always returns the supplied instant. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L48)

## Random

- module `Random`: Helpers for random-number capabilities. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L54)
- [`Random.nextInt`](./random-nextint.md): Reads a random integer from a random capability. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L56)
- [`Random.live`](./random-live.md): Creates a live random-number generator backed by `Random`. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L60)
- [`Random.fromValue`](./random-fromvalue.md): Creates a deterministic random generator that always returns the supplied value. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L69)

## GUID

- module `Guid`: Helpers for GUID capabilities. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L75)
- [`Guid.newGuid`](./guid-newguid.md): Reads a GUID from a GUID capability. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L77)
- [`Guid.live`](./guid-live.md): Creates a live GUID generator backed by `NewGuid`. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L80)
- [`Guid.fromValue`](./guid-fromvalue.md): Creates a deterministic GUID generator that always returns the supplied value. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L85)

## Environment variables

- module `EnvironmentVariables`: Helpers for environment-variable providers. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L91)
- [`EnvironmentVariables.tryGet`](./environmentvariables-tryget.md): Reads a raw environment-variable value from a provider. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L93)
- [`EnvironmentVariables.live`](./environmentvariables-live.md): Creates a live provider backed by the current process environment. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L97)
- [`EnvironmentVariables.fromPairs`](./environmentvariables-frompairs.md): Creates a deterministic provider from a fixed set of name/value pairs. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L105)
- module `EnvironmentVariable`: Helpers for reading and parsing environment variables. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L119)
- [`EnvironmentVariable.tryGet`](./environmentvariable-tryget.md): Reads a raw string environment variable without wrapping it in a result. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L140)
- [`EnvironmentVariable.get`](./environmentvariable-get.md): Reads a raw string environment variable. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L134)
- [`EnvironmentVariable.getInt`](./environmentvariable-getint.md): Reads an integer environment variable. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L144)
- [`EnvironmentVariable.getGuid`](./environmentvariable-getguid.md): Reads a GUID environment variable. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L151)
- [`EnvironmentVariable.getBool`](./environmentvariable-getbool.md): Reads a boolean environment variable. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L158)
- module `EnvironmentVariableErrors`: Helpers for formatting environment-variable errors. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L166)
- [`EnvironmentVariableErrors.describe`](./environmentvariableerrors-describe.md): Formats a human-readable description for an error. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow.Caps.Core/Core.fs#L168)

