---
title: TaskFlow Runtime
description: Source-documented task runtime helpers for FsFlow.
---

# TaskFlow Runtime

This page shows the source-documented task runtime surface: the runtime context and the task-specific operational helpers.

## Runtime context

- type [`RuntimeContext`](./runtimecontext.md): Captures the two-context shape of a task workflow execution:
runtime services, application capabilities, and the cancellation token for the current run. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L18)
- module `RuntimeContext`: Helpers for building and reshaping `RuntimeContext{runtime, env}` values. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L33)
- [`RuntimeContext.create`](./runtimecontext-create.md): Creates a runtime context from the supplied runtime services, environment, and cancellation token. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L39)
- [`RuntimeContext.runtime`](./runtimecontext-runtime.md): Reads the runtime half of a runtime context. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L53)
- [`RuntimeContext.environment`](./runtimecontext-environment.md): Reads the application environment half of a runtime context. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L58)
- [`RuntimeContext.cancellationToken`](./runtimecontext-cancellationtoken.md): Reads the cancellation token stored in a runtime context. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L63)
- [`RuntimeContext.mapRuntime`](./runtimecontext-mapruntime.md): Maps the runtime half of a runtime context. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L69)
- [`RuntimeContext.mapEnvironment`](./runtimecontext-mapenvironment.md): Maps the application environment half of a runtime context. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L83)
- [`RuntimeContext.withRuntime`](./runtimecontext-withruntime.md): Replaces the runtime half of a runtime context. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L97)
- [`RuntimeContext.withEnvironment`](./runtimecontext-withenvironment.md): Replaces the environment half of a runtime context. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L107)

## Runtime helpers

- module [`TaskFlow.Runtime`](./taskflow-runtime.md): Task-native runtime helpers for operational concerns like logging, timeout, retry, and scoped cleanup. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L586)
- [`TaskFlow.Runtime.cancellationToken`](./taskflow-runtime-cancellationtoken.md): Reads the current runtime cancellation token. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L588)
- [`TaskFlow.Runtime.catchCancellation`](./taskflow-runtime-catchcancellation.md): Converts an `OperationCanceledException` into a typed error. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L592)
- [`TaskFlow.Runtime.ensureNotCanceled`](./taskflow-runtime-ensurenotcanceled.md): Returns a typed error immediately when the runtime token is already canceled. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L605)
- [`TaskFlow.Runtime.sleep`](./taskflow-runtime-sleep.md): Suspends the flow for the specified duration while observing cancellation. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L613)
- [`TaskFlow.Runtime.log`](./taskflow-runtime-log.md): Writes a fixed log message through the environment-provided logger. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L621)
- [`TaskFlow.Runtime.logWith`](./taskflow-runtime-logwith.md): Writes a log message computed from the current environment. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L636)
- [`TaskFlow.Runtime.useWithAcquireRelease`](./taskflow-runtime-usewithacquirerelease.md): Acquires a resource, uses it, and always runs the release action. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L651)
- [`TaskFlow.Runtime.timeout`](./taskflow-runtime-timeout.md): Fails with the supplied error when the flow does not complete before the timeout. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L676)
- [`TaskFlow.Runtime.timeoutToOk`](./taskflow-runtime-timeouttook.md): Returns the supplied success value when the flow times out. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L694)
- [`TaskFlow.Runtime.timeoutToError`](./taskflow-runtime-timeouttoerror.md): Forwards to `timeout` for a typed failure on timeout. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L712)
- [`TaskFlow.Runtime.timeoutWith`](./taskflow-runtime-timeoutwith.md): Runs a fallback flow when the original flow times out. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L720)
- [`TaskFlow.Runtime.retry`](./taskflow-runtime-retry.md): Retries a flow according to the supplied policy. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L738)

