---
weight: 20
title: Execution Semantics
description: Exact execution rules for Flow.
type: docs
---


This page shows the exact execution rules for `Flow`, including how a cold program becomes an `Effect` and resolves to an `Exit`.

FsFlow uses a unified [**`Flow<'env, 'error, 'value>`**]({{< relref "/reference/flow/t-flow.md" >}}) model that handles synchronous code, F# `Async`, and .NET `Task` interop natively.

## Execution Shape

Conceptually, execution is:

`Flow -> Effect -> Exit`

More precisely:

- `Flow` is the cold program you define.
- `Effect` is the deferred runnable carrier.
- `Exit` is the terminal outcome after execution.

## Success and Typed Failure

Every Flow execution results in an [**`Exit<'value, 'error>`**]({{< relref "/reference/flow/t-flow.md" >}}):

- `Exit.Success value`: The happy path.
- `Exit.Failure (Cause.Fail error)`: An expected domain-specific failure.
- `Exit.Failure Cause.Interrupt`: The workflow was signaled to stop (e.g., cancellation).
- `Exit.Failure (Cause.Die exception)`: An unexpected defect or crash. `Flow.run`, `AsyncFlow.run`, and `TaskFlow.run` preserve uncaught exceptions in this branch.

All standard combinators (`map`, `bind`, `zip`, `orElse`) are **short-circuiting**. The first `Exit.Failure` stops the workflow unless explicitly caught.

## Short-Circuiting vs. Accumulated Validation

- [**`flow {}`**]({{< relref "/reference/flow/builders-flow.md" >}}) and **`result {}`**: Ordered workflows. They stop on the first failure.
- [**`validate {}`**]({{< relref "/reference/validation/builders-validate.md" >}}): Accumulating validation. Joins errors from independent steps (using `and!`) into a structured `Diagnostics` graph.

Use the short-circuiting model when later steps depend on earlier values. Use the validation model when independent checks should all run and report back together.

## Execution Is Explicit

You run a Flow by calling [`Flow.run`]({{< relref "/reference/flow/m-flow-run.md" >}}). 

`Flow.run` returns an **`Effect<'value, 'error>`**. The platform-specific carrier is defined by the target:

- On **.NET**: `Effect<'value, 'error>` is a `ValueTask<Exit<'value, 'error>>`.
- On **Fable**: `Effect<'value, 'error>` is an `Async<Exit<'value, 'error>>`.

This design allows FsFlow to remain portable while respecting the execution models of different platforms. `Effect` is the cross-platform execution handle.

A flow is **cold**: building a flow does not run it. Each call to `run` executes the logic from scratch.

## Why Exit Has Three Failure Causes

`Exit.Failure` carries a `Cause<'error>` instead of a plain error because not all failures mean the same thing.

- `Cause.Fail error`: An expected domain failure. This is the normal typed error path.
- `Cause.Interrupt`: A cancellation or interruption signal. The runtime uses this when a workflow stops because it was asked to stop.
- `Cause.Die exception`: An unexpected defect or crash. This is for bugs, panic-like failures, and exceptions that were not intentionally translated into a typed error or explicitly constructed with `Flow.die`.

This split matters because one generic failure type cannot answer three different questions:

- Was this a business-rule failure that should participate in normal control flow?
- Was this a cancellation signal that should stop work immediately and usually not be retried?
- Was this a defect that should usually surface as a crash or be logged as an unexpected exception?

### Why Not Just Use `CancellationToken`?

`CancellationToken` only models interruption. It does not model:

- a typed domain error like `Cause.Fail`
- a defect like `Cause.Die`
- the fact that a flow can fail for reasons other than cancellation

FsFlow still uses `CancellationToken` internally and for interoperability, but it converts cancellation into `Cause.Interrupt` at the edges where the runtime can observe it. That keeps cancellation as a first-class execution outcome instead of burying it inside the ambient .NET token API.

### How Each Cause Gets Produced

- `Cause.Fail` is produced by explicit domain failures such as `Flow.error`, `Flow.fail`, `Flow.fromResult`, and validation branches that intentionally turn a rejected condition into a typed error.
- `Cause.Interrupt` is produced when the runtime observes cancellation or interruption, such as `Flow.interrupt` or a cancellation-aware helper like `Flow.Runtime.sleep`.
- `Cause.Die` is produced by defects that escape normal typed handling, such as unexpected exceptions, or by an explicit `Flow.die` constructor. The runtime preserves the defect unless you intentionally catch and translate it.

### Where The Runtime Helps

The runtime already does some of the translation work:

- runtime boundaries classify uncaught `OperationCanceledException` values as `Cause.Interrupt`
- cancellation-aware helpers convert `OperationCanceledException` into `Cause.Interrupt`
- exception-catching helpers like `Flow.catch` translate exceptions into `Cause.Fail` when you want them treated as domain errors
- `Exit.toResult` re-raises `Cause.Die` and `Cause.Interrupt` when you collapse back into a plain `Result`, because those are not normal success-path errors
- `Schedule`-based retry helpers retry only `Cause.Fail`; defects and interruptions pass through unchanged

## Interruption and Cancellation

FsFlow supports algebraic interruption. When a fiber is interrupted (e.g., via `Flow.interrupt` or a `CancellationToken` trigger), the flow stops executing and returns `Exit.Failure Cause.Interrupt`.

## Environments

Flow reads dependencies explicitly:

- `Flow.env`: Reads the whole environment.
- `Flow.read`: Projects one dependency.
- `Flow.localEnv`: Runs a smaller computation inside a larger environment.

## Task Temperature

Flow distinguishes between:

- **Hot Inputs**: Already-started `Task<'value>` or `Async<'value>`. Re-running the flow re-awaits the same underlying work.
- **Cold Inputs**: Logic defined inside `flow {}` or helpers like `Flow.Runtime.sleep`. Re-running the flow repeats the work.

Use Cold inputs when you want the effect to observe the runtime `CancellationToken` or repeat its side effects on retry.

## Runtime Helpers

Operational helpers for logging, timeout, retry, and resource handling are grouped into the `Flow.Runtime` and `Schedule` modules.

## Next

Read [Defects and Exceptions](./defects.md) for the defect-model rationale, [Getting Started](../start/getting-started/) for a tutorial-style overview, or browse the [API Reference](../../reference/) for detailed signatures.
