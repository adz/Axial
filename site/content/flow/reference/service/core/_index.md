---
title: "Services Core"
weight: 10
type: docs
---

This page shows the core service package: clock, logging, random numbers, GUID generation, and environment-variable lookup. These are explicit services, not ambient runtime slots. Use the helper modules when a workflow needs one of these services, and use `BaseRuntime` or custom environments to supply deterministic or live implementations.

## Service types

- [`Flow.PlatformService.IClock`](./t-flow-platformservice-iclock.md): Provides synchronous access to the current UTC clock.
- [`Flow.PlatformService.ILog`](./t-flow-platformservice-ilog.md): Provides synchronous access to workflow logging as an explicit service.
- [`Flow.LogLevel`](./t-flow-loglevel.md):  Log levels used by runtime logging helpers and environment-provided logging functions.
- [`Flow.PlatformService.IRandom`](./t-flow-platformservice-irandom.md): Provides synchronous random-number generation.
- [`Flow.PlatformService.IGuid`](./t-flow-platformservice-iguid.md): Provides synchronous GUID generation.
- [`Flow.PlatformService.IEnvironmentVariables`](./t-flow-platformservice-ienvironmentvariables.md): Provides environment-variable access supplied by the application host.
- [`Flow.PlatformService.EnvironmentVariableError`](./t-flow-platformservice-environmentvariableerror.md):
- [`Flow.PlatformService.BaseRuntimeError`](./t-flow-platformservice-baseruntimeerror.md):
- [`Flow.PlatformService.BaseRuntime`](./t-flow-platformservice-baseruntime.md): Groups the standard operational services commonly used by workflow hosts.

## Base runtime

- [`Flow.PlatformService.BaseRuntime.liveValue`](./base-runtime/p-flow-platformservice-baseruntime-livevalue.md): Creates the standard live base runtime as an explicit service bundle.
- [`Flow.PlatformService.BaseRuntime.live`](./base-runtime/p-flow-platformservice-baseruntime-live.md): Builds the standard live base runtime as an explicit service bundle.
- [`Flow.PlatformService.BaseRuntime.fromServiceProvider`](./base-runtime/p-flow-platformservice-baseruntime-fromserviceprovider.md): Builds the base runtime from an <a href="https://learn.microsoft.com/dotnet/api/system.iserviceprovider">IServiceProvider</a>.

## Clock

- [`Flow.PlatformService.Clock.now`](./clock/m-flow-platformservice-clock-now.md): Reads the current UTC timestamp from an explicit clock service.
- [`Flow.PlatformService.Clock.utcDateTime`](./clock/m-flow-platformservice-clock-utcdatetime.md): Reads the current UTC date/time from an explicit clock service.
- [`Flow.PlatformService.Clock.unixTimeSeconds`](./clock/m-flow-platformservice-clock-unixtimeseconds.md): Reads the current Unix timestamp in seconds from an explicit clock service.
- [`Flow.PlatformService.Clock.unixTimeMilliseconds`](./clock/m-flow-platformservice-clock-unixtimemilliseconds.md): Reads the current Unix timestamp in milliseconds from an explicit clock service.
- [`Flow.PlatformService.Clock.live`](./clock/p-flow-platformservice-clock-live.md): Creates a live clock backed by <a href="https://learn.microsoft.com/dotnet/api/system.datetimeoffset.utcnow">DateTimeOffset.UtcNow</a>.
- [`Flow.PlatformService.Clock.layer`](./clock/p-flow-platformservice-clock-layer.md): Builds the live clock as a layer.
- [`Flow.PlatformService.Clock.fromValue`](./clock/m-flow-platformservice-clock-fromvalue.md): Creates a deterministic clock that always returns the supplied instant.

## Logging

- [`Flow.PlatformService.Log.log`](./log/m-flow-platformservice-log-log.md): Writes a log message at the requested level through an explicit logging service.
- [`Flow.PlatformService.Log.trace`](./log/m-flow-platformservice-log-trace.md): Writes a trace log message through an explicit logging service.
- [`Flow.PlatformService.Log.debug`](./log/m-flow-platformservice-log-debug.md): Writes a debug log message through an explicit logging service.
- [`Flow.PlatformService.Log.info`](./log/m-flow-platformservice-log-info.md): Writes an informational log message through an explicit logging service.
- [`Flow.PlatformService.Log.warning`](./log/m-flow-platformservice-log-warning.md): Writes a warning log message through an explicit logging service.
- [`Flow.PlatformService.Log.error`](./log/m-flow-platformservice-log-error.md): Writes an error log message through an explicit logging service.
- [`Flow.PlatformService.Log.critical`](./log/m-flow-platformservice-log-critical.md): Writes a critical log message through an explicit logging service.
- [`Flow.PlatformService.Log.live`](./log/p-flow-platformservice-log-live.md): Creates a no-op logger for tests and local service bundles.
- [`Flow.PlatformService.Log.layer`](./log/p-flow-platformservice-log-layer.md): Builds the live logger as a layer.
- [`Flow.PlatformService.Log.fromSink`](./log/m-flow-platformservice-log-fromsink.md): Creates a logger from a synchronous sink function.

## Random

- [`Flow.PlatformService.Random.next`](./random/m-flow-platformservice-random-next.md): Reads a non-negative random integer from an explicit random-number service.
- [`Flow.PlatformService.Random.nextMax`](./random/m-flow-platformservice-random-nextmax.md): Reads a random integer less than the supplied maximum from an explicit random-number service.
- [`Flow.PlatformService.Random.nextInt`](./random/m-flow-platformservice-random-nextint.md): Reads a random integer from an explicit random-number service.
- [`Flow.PlatformService.Random.nextDouble`](./random/m-flow-platformservice-random-nextdouble.md): Reads a random floating-point value from an explicit random-number service.
- [`Flow.PlatformService.Random.nextBytes`](./random/m-flow-platformservice-random-nextbytes.md): Fills a byte buffer through an explicit random-number service.
- [`Flow.PlatformService.Random.bytes`](./random/m-flow-platformservice-random-bytes.md): Creates a byte array filled through an explicit random-number service.
- [`Flow.PlatformService.Random.live`](./random/p-flow-platformservice-random-live.md): Creates a live random-number generator backed by <a href="https://learn.microsoft.com/dotnet/api/system.random">Random</a>.
- [`Flow.PlatformService.Random.layer`](./random/p-flow-platformservice-random-layer.md): Builds the live random-number generator as a layer.
- [`Flow.PlatformService.Random.fromValue`](./random/m-flow-platformservice-random-fromvalue.md): Creates a deterministic random generator that always returns the supplied value.
- [`Flow.PlatformService.Random.fromFixed`](./random/m-flow-platformservice-random-fromfixed.md): Creates a deterministic random generator from fixed integer, double, and byte values.

## GUID

- [`Flow.PlatformService.Guid.newGuid`](./guid/m-flow-platformservice-guid-newguid.md): Reads a GUID from an explicit GUID service.
- [`Flow.PlatformService.Guid.live`](./guid/p-flow-platformservice-guid-live.md): Creates a live GUID service backed by <code>Guid.NewGuid()</code>.
- [`Flow.PlatformService.Guid.layer`](./guid/p-flow-platformservice-guid-layer.md): Builds the live GUID service as a layer.
- [`Flow.PlatformService.Guid.fromValue`](./guid/m-flow-platformservice-guid-fromvalue.md): Creates a deterministic GUID service that always returns the supplied value.

## Environment variables

- [`Flow.PlatformService.EnvironmentVariables.tryGet`](./environment-variables/m-flow-platformservice-environmentvariables-tryget.md): Reads a raw environment-variable value from an explicit environment-variable service.
- [`Flow.PlatformService.EnvironmentVariables.getAll`](./environment-variables/m-flow-platformservice-environmentvariables-getall.md): Returns all visible environment variables from an explicit environment-variable service.
- [`Flow.PlatformService.EnvironmentVariables.set`](./environment-variables/m-flow-platformservice-environmentvariables-set.md): Sets or updates an environment variable through an explicit environment-variable service.
- [`Flow.PlatformService.EnvironmentVariables.clear`](./environment-variables/m-flow-platformservice-environmentvariables-clear.md): Clears an environment variable through an explicit environment-variable service.
- [`Flow.PlatformService.EnvironmentVariables.expand`](./environment-variables/m-flow-platformservice-environmentvariables-expand.md): Expands environment-variable references in text through an explicit environment-variable service.
- [`Flow.PlatformService.EnvironmentVariables.live`](./environment-variables/p-flow-platformservice-environmentvariables-live.md): Creates a live provider backed by the current process environment.
- [`Flow.PlatformService.EnvironmentVariables.layer`](./environment-variables/p-flow-platformservice-environmentvariables-layer.md): Builds the live environment-variable service as a layer.
- [`Flow.PlatformService.EnvironmentVariables.fromPairs`](./environment-variables/m-flow-platformservice-environmentvariables-frompairs.md): Creates a deterministic provider from a fixed set of name/value pairs.
- [`Flow.PlatformService.EnvironmentVariable.tryGet`](./environment-variables/m-flow-platformservice-environmentvariable-tryget.md): Reads a raw string environment variable without wrapping it in a result.
- [`Flow.PlatformService.EnvironmentVariable.get`](./environment-variables/m-flow-platformservice-environmentvariable-get.md): Reads a raw string environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariable.getInt`](./environment-variables/m-flow-platformservice-environmentvariable-getint.md): Reads an integer environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariable.getInt64`](./environment-variables/m-flow-platformservice-environmentvariable-getint64.md): Reads a 64-bit integer environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariable.getDouble`](./environment-variables/m-flow-platformservice-environmentvariable-getdouble.md): Reads a floating-point environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariable.getDecimal`](./environment-variables/m-flow-platformservice-environmentvariable-getdecimal.md): Reads a decimal environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariable.getGuid`](./environment-variables/m-flow-platformservice-environmentvariable-getguid.md): Reads a GUID environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariable.getUri`](./environment-variables/m-flow-platformservice-environmentvariable-geturi.md): Reads a URI environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariable.getTimeSpan`](./environment-variables/m-flow-platformservice-environmentvariable-gettimespan.md): Reads a time span environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariable.getBool`](./environment-variables/m-flow-platformservice-environmentvariable-getbool.md): Reads a boolean environment variable through an explicit service.
- [`Flow.PlatformService.EnvironmentVariableErrors.describe`](./environment-variables/m-flow-platformservice-environmentvariableerrors-describe.md): Formats a human-readable description for an error.
