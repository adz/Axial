---
title: "Runtime"
type: docs
weight: 90
---

This page shows the source-documented `AsyncFlow.Runtime` surface: logging, retry policies, and async operational helpers.

## Logging

- type [`LogLevel`](./loglevel.md): Log levels used by runtime logging helpers and environment-provided logging functions. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L32)
### Constructors

- `Trace` [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L33)
- `Debug` [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L34)
- `Information` [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L35)
- `Warning` [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L36)
- `Error` [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L37)
- `Critical` [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L38)

- type [`LogEntry`](./logentry.md): A structured log entry written through a runtime logger. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L43)

## Retry policy

- type [`RetryPolicy`](./retrypolicy.md): Defines how runtime retry helpers repeat typed failures in a controlled way. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L53)
- module `RetryPolicy`: Standard retry policies for runtime helpers. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L64)
- [`RetryPolicy.noDelay`](./retrypolicy-nodelay.md) [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L65)

## Async operational helpers

- module [`AsyncFlow.Runtime`](./runtime.md): Runtime helpers for operational concerns like logging, timeout, retry, and cleanup. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L361)
- [`AsyncFlow.Runtime.cancellationToken`](./asyncflow-runtime-cancellationtoken.md): Reads the current cancellation token from the flow. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L369)
- [`AsyncFlow.Runtime.catchCancellation`](./asyncflow-runtime-catchcancellation.md): Catches `OperationCanceledException` and converts it into a typed error. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L382)
- [`AsyncFlow.Runtime.ensureNotCanceled`](./asyncflow-runtime-ensurenotcanceled.md): Checks if cancellation has been requested and returns a typed error if it has. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L399)
- [`AsyncFlow.Runtime.sleep`](./asyncflow-runtime-sleep.md): Suspends the flow for the specified duration, observing cancellation. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L415)
- [`AsyncFlow.Runtime.log`](./asyncflow-runtime-log.md): Writes a log entry using the writer provided by the environment. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L429)
- [`AsyncFlow.Runtime.logWith`](./asyncflow-runtime-logwith.md): Writes a log entry using a message produced from the environment. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L451)
- [`AsyncFlow.Runtime.useWithAcquireRelease`](./asyncflow-runtime-usewithacquirerelease.md): Safely acquires a resource, uses it, and ensures it is released via a task-based action. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L473)
- [`AsyncFlow.Runtime.timeout`](./asyncflow-runtime-timeout.md): Wraps a flow with a timeout. If the flow does not complete within the specified duration, returns a typed error. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L501)
- [`AsyncFlow.Runtime.timeoutToOk`](./asyncflow-runtime-timeouttook.md): Wraps a flow with a timeout. If the flow does not complete within the specified duration, returns a success value. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L525)
- [`AsyncFlow.Runtime.timeoutToError`](./asyncflow-runtime-timeouttoerror.md): Transitions to a failure value on timeout. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L549)
- [`AsyncFlow.Runtime.timeoutWith`](./asyncflow-runtime-timeoutwith.md): Transitions to a fallback workflow on timeout. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L559)
- [`AsyncFlow.Runtime.retry`](./asyncflow-runtime-retry.md): Retries a flow according to the specified policy. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L585)

