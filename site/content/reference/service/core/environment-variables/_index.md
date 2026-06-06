---
title: "Environment variables"
type: docs
---

This page shows the `Core.EnvironmentVariables`, `Core.EnvironmentVariable`, and `Core.EnvironmentVariableErrors` helpers for explicit environment-variable access.

- [`Core.EnvironmentVariables.tryGet`](./m-core-environmentvariables-tryget.md): Reads a raw environment-variable value from an explicit environment-variable service.
- [`Core.EnvironmentVariables.getAll`](./m-core-environmentvariables-getall.md): Returns all visible environment variables from an explicit environment-variable service.
- [`Core.EnvironmentVariables.set`](./m-core-environmentvariables-set.md): Sets or updates an environment variable through an explicit environment-variable service.
- [`Core.EnvironmentVariables.clear`](./m-core-environmentvariables-clear.md): Clears an environment variable through an explicit environment-variable service.
- [`Core.EnvironmentVariables.expand`](./m-core-environmentvariables-expand.md): Expands environment-variable references in text through an explicit environment-variable service.
- [`Core.EnvironmentVariables.live`](./p-core-environmentvariables-live.md): Creates a live provider backed by the current process environment.
- [`Core.EnvironmentVariables.layer`](./p-core-environmentvariables-layer.md): Builds the live environment-variable service as a layer.
- [`Core.EnvironmentVariables.fromPairs`](./m-core-environmentvariables-frompairs.md): Creates a deterministic provider from a fixed set of name/value pairs.
- [`Core.EnvironmentVariable.tryGet`](./m-core-environmentvariable-tryget.md): Reads a raw string environment variable without wrapping it in a result.
- [`Core.EnvironmentVariable.get`](./m-core-environmentvariable-get.md): Reads a raw string environment variable through an explicit service.
- [`Core.EnvironmentVariable.getInt`](./m-core-environmentvariable-getint.md): Reads an integer environment variable through an explicit service.
- [`Core.EnvironmentVariable.getInt64`](./m-core-environmentvariable-getint64.md): Reads a 64-bit integer environment variable through an explicit service.
- [`Core.EnvironmentVariable.getDouble`](./m-core-environmentvariable-getdouble.md): Reads a floating-point environment variable through an explicit service.
- [`Core.EnvironmentVariable.getDecimal`](./m-core-environmentvariable-getdecimal.md): Reads a decimal environment variable through an explicit service.
- [`Core.EnvironmentVariable.getGuid`](./m-core-environmentvariable-getguid.md): Reads a GUID environment variable through an explicit service.
- [`Core.EnvironmentVariable.getUri`](./m-core-environmentvariable-geturi.md): Reads a URI environment variable through an explicit service.
- [`Core.EnvironmentVariable.getTimeSpan`](./m-core-environmentvariable-gettimespan.md): Reads a time span environment variable through an explicit service.
- [`Core.EnvironmentVariable.getBool`](./m-core-environmentvariable-getbool.md): Reads a boolean environment variable through an explicit service.
- [`Core.EnvironmentVariableErrors.describe`](./m-core-environmentvariableerrors-describe.md): Formats a human-readable description for an error.
