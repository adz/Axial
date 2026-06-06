---
title: "Services Core"
weight: 10
---

This page shows the core service package: clock, logging, random numbers, GUID generation, and environment-variable lookup. These are explicit services, not ambient runtime slots. Use the helper modules when a workflow needs one of these services, and use `BaseRuntime` or custom environments to supply deterministic or live implementations.

## Service types

- [`IClock`](./t-core-iclock.md): Provides synchronous access to the current UTC clock.
- [`ILog`](./t-core-ilog.md): Provides synchronous access to explicit workflow logging.
- [`IRandom`](./t-core-irandom.md): Provides synchronous random-number generation.
- [`IGuid`](./t-core-iguid.md): Provides synchronous GUID generation.
- [`IEnvironmentVariables`](./t-core-ienvironmentvariables.md): Provides synchronous environment-variable access.
- [`Core.EnvironmentVariableError`](./t-core-environmentvariableerror.md): Describes a meaningful environment-variable failure.
- [`Core.BaseRuntimeError`](./t-core-baseruntimeerror.md): Describes a service-provider bootstrap failure while building the base runtime.
- [`Core.BaseRuntime`](./t-core-baseruntime.md): Helpers for constructing the standard explicit service bundle used by workflow hosts.

## Base runtime

- [`Core.BaseRuntime.liveValue`](./base-runtime/p-core-baseruntime-livevalue.md): Creates the standard live base runtime as an explicit service bundle.
- [`Core.BaseRuntime.live`](./base-runtime/p-core-baseruntime-live.md): Builds the standard live base runtime as an explicit service bundle.
- [`Core.BaseRuntime.fromServiceProvider`](./base-runtime/p-core-baseruntime-fromserviceprovider.md): Builds the base runtime from an <a href="https://learn.microsoft.com/dotnet/api/system.iserviceprovider">IServiceProvider</a>.

## Clock

- [`Core.Clock.now`](./clock/m-core-clock-now.md): Reads the current UTC timestamp from an explicit clock service.
- [`Core.Clock.utcDateTime`](./clock/m-core-clock-utcdatetime.md): Reads the current UTC date/time from an explicit clock service.
- [`Core.Clock.unixTimeSeconds`](./clock/m-core-clock-unixtimeseconds.md): Reads the current Unix timestamp in seconds from an explicit clock service.
- [`Core.Clock.unixTimeMilliseconds`](./clock/m-core-clock-unixtimemilliseconds.md): Reads the current Unix timestamp in milliseconds from an explicit clock service.
- [`Core.Clock.live`](./clock/p-core-clock-live.md): Creates a live clock backed by <a href="https://learn.microsoft.com/dotnet/api/system.datetimeoffset.utcnow">DateTimeOffset.UtcNow</a>.
- [`Core.Clock.layer`](./clock/p-core-clock-layer.md): Builds the live clock as a layer.
- [`Core.Clock.fromValue`](./clock/m-core-clock-fromvalue.md): Creates a deterministic clock that always returns the supplied instant.

## Logging

- [`Core.Log.log`](./log/m-core-log-log.md): Writes a log message at the requested level through an explicit logging service.
- [`Core.Log.trace`](./log/m-core-log-trace.md): Writes a trace log message through an explicit logging service.
- [`Core.Log.debug`](./log/m-core-log-debug.md): Writes a debug log message through an explicit logging service.
- [`Core.Log.info`](./log/m-core-log-info.md): Writes an informational log message through an explicit logging service.
- [`Core.Log.warning`](./log/m-core-log-warning.md): Writes a warning log message through an explicit logging service.
- [`Core.Log.error`](./log/m-core-log-error.md): Writes an error log message through an explicit logging service.
- [`Core.Log.critical`](./log/m-core-log-critical.md): Writes a critical log message through an explicit logging service.
- [`Core.Log.live`](./log/p-core-log-live.md): Creates a no-op logger for tests and local service bundles.
- [`Core.Log.layer`](./log/p-core-log-layer.md): Builds the live logger as a layer.
- [`Core.Log.fromSink`](./log/m-core-log-fromsink.md): Creates a logger from a synchronous sink function.

## Random

- [`Core.Random.next`](./random/m-core-random-next.md): Reads a non-negative random integer from an explicit random-number service.
- [`Core.Random.nextMax`](./random/m-core-random-nextmax.md): Reads a random integer less than the supplied maximum from an explicit random-number service.
- [`Core.Random.nextInt`](./random/m-core-random-nextint.md): Reads a random integer from an explicit random-number service.
- [`Core.Random.nextDouble`](./random/m-core-random-nextdouble.md): Reads a random floating-point value from an explicit random-number service.
- [`Core.Random.nextBytes`](./random/m-core-random-nextbytes.md): Fills a byte buffer through an explicit random-number service.
- [`Core.Random.bytes`](./random/m-core-random-bytes.md): Creates a byte array filled through an explicit random-number service.
- [`Core.Random.live`](./random/p-core-random-live.md): Creates a live random-number generator backed by <a href="https://learn.microsoft.com/dotnet/api/system.random">Random</a>.
- [`Core.Random.layer`](./random/p-core-random-layer.md): Builds the live random-number generator as a layer.
- [`Core.Random.fromValue`](./random/m-core-random-fromvalue.md): Creates a deterministic random generator that always returns the supplied value.
- [`Core.Random.fromFixed`](./random/m-core-random-fromfixed.md): Creates a deterministic random generator from fixed integer, double, and byte values.

## GUID

- [`Core.Guid.newGuid`](./guid/m-core-guid-newguid.md): Reads a GUID from an explicit GUID service.
- [`Core.Guid.live`](./guid/p-core-guid-live.md): Creates a live GUID service backed by <code>Guid.NewGuid()</code>.
- [`Core.Guid.layer`](./guid/p-core-guid-layer.md): Builds the live GUID service as a layer.
- [`Core.Guid.fromValue`](./guid/m-core-guid-fromvalue.md): Creates a deterministic GUID service that always returns the supplied value.

## Environment variables

- [`Core.EnvironmentVariables.tryGet`](./environment-variables/m-core-environmentvariables-tryget.md): Reads a raw environment-variable value from an explicit environment-variable service.
- [`Core.EnvironmentVariables.getAll`](./environment-variables/m-core-environmentvariables-getall.md): Returns all visible environment variables from an explicit environment-variable service.
- [`Core.EnvironmentVariables.set`](./environment-variables/m-core-environmentvariables-set.md): Sets or updates an environment variable through an explicit environment-variable service.
- [`Core.EnvironmentVariables.clear`](./environment-variables/m-core-environmentvariables-clear.md): Clears an environment variable through an explicit environment-variable service.
- [`Core.EnvironmentVariables.expand`](./environment-variables/m-core-environmentvariables-expand.md): Expands environment-variable references in text through an explicit environment-variable service.
- [`Core.EnvironmentVariables.live`](./environment-variables/p-core-environmentvariables-live.md): Creates a live provider backed by the current process environment.
- [`Core.EnvironmentVariables.layer`](./environment-variables/p-core-environmentvariables-layer.md): Builds the live environment-variable service as a layer.
- [`Core.EnvironmentVariables.fromPairs`](./environment-variables/m-core-environmentvariables-frompairs.md): Creates a deterministic provider from a fixed set of name/value pairs.
- [`Core.EnvironmentVariable.tryGet`](./environment-variables/m-core-environmentvariable-tryget.md): Reads a raw string environment variable without wrapping it in a result.
- [`Core.EnvironmentVariable.get`](./environment-variables/m-core-environmentvariable-get.md): Reads a raw string environment variable through an explicit service.
- [`Core.EnvironmentVariable.getInt`](./environment-variables/m-core-environmentvariable-getint.md): Reads an integer environment variable through an explicit service.
- [`Core.EnvironmentVariable.getInt64`](./environment-variables/m-core-environmentvariable-getint64.md): Reads a 64-bit integer environment variable through an explicit service.
- [`Core.EnvironmentVariable.getDouble`](./environment-variables/m-core-environmentvariable-getdouble.md): Reads a floating-point environment variable through an explicit service.
- [`Core.EnvironmentVariable.getDecimal`](./environment-variables/m-core-environmentvariable-getdecimal.md): Reads a decimal environment variable through an explicit service.
- [`Core.EnvironmentVariable.getGuid`](./environment-variables/m-core-environmentvariable-getguid.md): Reads a GUID environment variable through an explicit service.
- [`Core.EnvironmentVariable.getUri`](./environment-variables/m-core-environmentvariable-geturi.md): Reads a URI environment variable through an explicit service.
- [`Core.EnvironmentVariable.getTimeSpan`](./environment-variables/m-core-environmentvariable-gettimespan.md): Reads a time span environment variable through an explicit service.
- [`Core.EnvironmentVariable.getBool`](./environment-variables/m-core-environmentvariable-getbool.md): Reads a boolean environment variable through an explicit service.
- [`Core.EnvironmentVariableErrors.describe`](./environment-variables/m-core-environmentvariableerrors-describe.md): Formats a human-readable description for an error.
