---
title: "Environment variables"
---

This page shows the `Core.EnvironmentVariables`, `Core.EnvironmentVariable`, and `Core.EnvironmentVariableErrors` helpers for explicit environment-variable access.

- [`Flow.PlatformService.EnvironmentVariables.tryGet`](./m-flow-platformservice-environmentvariables-tryget.md): Reads a raw environment-variable value from an explicit environment-variable service.
- [`Flow.PlatformService.EnvironmentVariables.getAll`](./m-flow-platformservice-environmentvariables-getall.md): Returns all visible environment variables from an explicit environment-variable service.
- [`Flow.PlatformService.EnvironmentVariables.set`](./m-flow-platformservice-environmentvariables-set.md): Sets or updates an environment variable through an explicit environment-variable service.
- [`Flow.PlatformService.EnvironmentVariables.clear`](./m-flow-platformservice-environmentvariables-clear.md): Clears an environment variable through an explicit environment-variable service.
- [`Flow.PlatformService.EnvironmentVariables.expand`](./m-flow-platformservice-environmentvariables-expand.md): Expands environment-variable references in text through an explicit environment-variable service.
- [`Flow.PlatformService.EnvironmentVariables.live`](./p-flow-platformservice-environmentvariables-live.md): Creates a live provider backed by the current process environment.
- [`Flow.PlatformService.EnvironmentVariables.layer`](./p-flow-platformservice-environmentvariables-layer.md): Builds the live environment-variable service as a layer.
- [`Flow.PlatformService.EnvironmentVariables.fromPairs`](./m-flow-platformservice-environmentvariables-frompairs.md): Creates a deterministic provider from a fixed set of name/value pairs.
- [`Flow.PlatformService.EnvironmentVariable.tryGet`](./m-flow-platformservice-environmentvariable-tryget.md): Reads a raw string environment variable without wrapping it in a result.
- [`Flow.PlatformService.EnvironmentVariable.get`](./m-flow-platformservice-environmentvariable-get.md): Reads a raw string environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariable.getInt`](./m-flow-platformservice-environmentvariable-getint.md): Reads an integer environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariable.getInt64`](./m-flow-platformservice-environmentvariable-getint64.md): Reads a 64-bit integer environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariable.getDouble`](./m-flow-platformservice-environmentvariable-getdouble.md): Reads a floating-point environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariable.getDecimal`](./m-flow-platformservice-environmentvariable-getdecimal.md): Reads a decimal environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariable.getGuid`](./m-flow-platformservice-environmentvariable-getguid.md): Reads a GUID environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariable.getUri`](./m-flow-platformservice-environmentvariable-geturi.md): Reads a URI environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariable.getTimeSpan`](./m-flow-platformservice-environmentvariable-gettimespan.md): Reads a time span environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariable.getBool`](./m-flow-platformservice-environmentvariable-getbool.md): Reads a boolean environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariableErrors.describe`](./m-flow-platformservice-environmentvariableerrors-describe.md): Formats a human-readable description for an error.
