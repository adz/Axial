---
title: "Services Core"
weight: 10
type: docs
---

This page shows the core service package: clock, logging, random numbers, GUID generation, and environment-variable lookup. These are explicit services, not ambient runtime slots. Use the helper modules when a workflow needs one of these services, and use `BaseRuntime` or custom environments to supply deterministic or live implementations.

## Service types

- [`IClock`](./t-core-iclock.md): Provides synchronous access to the current UTC clock.
- [`ILog`](./t-core-ilog.md): Provides synchronous access to runtime logging.
- [`IRandom`](./t-core-irandom.md): Provides synchronous random-number generation.
- [`IGuid`](./t-core-iguid.md): Provides synchronous GUID generation.
- [`IEnvironmentVariables`](./t-core-ienvironmentvariables.md): Provides synchronous environment-variable lookup.
- [`Core.EnvironmentVariableError`](./t-core-environmentvariableerror.md): Describes a meaningful environment-variable failure.
- [`Core.BaseRuntimeError`](./t-core-baseruntimeerror.md): Describes a service-provider bootstrap failure while building the base runtime.
- [`Core.BaseRuntime`](./t-core-baseruntime.md): Helpers for constructing the standard explicit service bundle used by workflow hosts.

## Base runtime

- [`Core.BaseRuntime.liveValue`](./p-core-baseruntime-livevalue.md): Creates the standard live base runtime as an explicit service bundle.
- [`Core.BaseRuntime.live`](./p-core-baseruntime-live.md): Builds the standard live base runtime as an explicit service bundle.
- [`Core.BaseRuntime.fromServiceProvider`](./p-core-baseruntime-fromserviceprovider.md): Builds the base runtime from an <a href="https://learn.microsoft.com/dotnet/api/system.iserviceprovider">IServiceProvider</a>.

## Clock

- [`Core.Clock.now`](./m-core-clock-now.md): Reads the current UTC timestamp from an explicit clock service.
- [`Core.Clock.live`](./p-core-clock-live.md): Creates a live clock backed by <a href="https://learn.microsoft.com/dotnet/api/system.datetimeoffset.utcnow">DateTimeOffset.UtcNow</a>.
- [`layer`](./p-core-clock-layer.md):
 The <code>layer { }</code> computation expression for provisioning explicit service environments.

- [`Core.Clock.fromValue`](./m-core-clock-fromvalue.md): Creates a deterministic clock that always returns the supplied instant.

## Logging

- [`Core.Log.info`](./m-core-log-info.md): Writes an informational log message through an explicit logging service.
- [`Core.Log.live`](./p-core-log-live.md): Creates a no-op logger for tests and local service bundles.
- [`layer`](./p-core-log-layer.md):
 The <code>layer { }</code> computation expression for provisioning explicit service environments.


## Random

- [`Core.Random.nextInt`](./m-core-random-nextint.md): Reads a random integer from an explicit random-number service.
- [`Core.Random.live`](./p-core-random-live.md): Creates a live random-number generator backed by <a href="https://learn.microsoft.com/dotnet/api/system.random">Random</a>.
- [`layer`](./p-core-random-layer.md):
 The <code>layer { }</code> computation expression for provisioning explicit service environments.

- [`Core.Random.fromValue`](./m-core-random-fromvalue.md): Creates a deterministic random generator that always returns the supplied value.

## GUID

- [`Core.Guid.newGuid`](./m-core-guid-newguid.md): Reads a GUID from an explicit GUID service.
- [`Core.Guid.live`](./p-core-guid-live.md): Creates a live GUID service backed by <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-services-core-guid.html">Guid.NewGuid</a>.
- [`layer`](./p-core-guid-layer.md):
 The <code>layer { }</code> computation expression for provisioning explicit service environments.

- [`Core.Guid.fromValue`](./m-core-guid-fromvalue.md): Creates a deterministic GUID service that always returns the supplied value.

## Environment variables

- [`Core.EnvironmentVariables.tryGet`](./m-core-environmentvariables-tryget.md): Reads a raw environment-variable value from an explicit environment-variable service.
- [`Core.EnvironmentVariables.live`](./p-core-environmentvariables-live.md): Creates a live provider backed by the current process environment.
- [`layer`](./p-core-environmentvariables-layer.md):
 The <code>layer { }</code> computation expression for provisioning explicit service environments.

- [`Core.EnvironmentVariables.fromPairs`](./m-core-environmentvariables-frompairs.md): Creates a deterministic provider from a fixed set of name/value pairs.
- [`Core.EnvironmentVariable.tryGet`](./m-core-environmentvariable-tryget.md): Reads a raw string environment variable without wrapping it in a result.
- [`Core.EnvironmentVariable.get`](./m-core-environmentvariable-get.md): Reads a raw string environment variable through an explicit service.
- [`Core.EnvironmentVariable.getInt`](./m-core-environmentvariable-getint.md): Reads an integer environment variable through an explicit service.
- [`Core.EnvironmentVariable.getGuid`](./m-core-environmentvariable-getguid.md): Reads a GUID environment variable through an explicit service.
- [`Core.EnvironmentVariable.getBool`](./m-core-environmentvariable-getbool.md): Reads a boolean environment variable through an explicit service.
- [`Core.EnvironmentVariableErrors.describe`](./m-core-environmentvariableerrors-describe.md): Formats a human-readable description for an error.
