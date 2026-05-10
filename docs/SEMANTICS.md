---
weight: 20
title: Execution Semantics
description: Exact execution rules for Flow.
---

# Execution Semantics

FsFlow uses a unified [**`Flow<'env, 'error, 'value>`**]({{< relref "/reference/flow/t-flow.md" >}}) model that handles synchronous code, F# `Async`, and .NET `Task` interop natively.

## Success and Typed Failure

Every Flow execution results in an [**`Exit<'value, 'error>`**]({{< relref "/reference/flow/t-flow.md" >}}):

- `Exit.Success value`: The happy path.
- `Exit.Failure (Cause.Fail error)`: An expected domain-specific failure.
- `Exit.Failure Cause.Interrupt`: The workflow was signaled to stop (e.g., cancellation).
- `Exit.Failure (Cause.Die exception)`: An unexpected defect or crash.

All standard combinators (`map`, `bind`, `zip`, `orElse`) are **short-circuiting**. The first `Exit.Failure` stops the workflow unless explicitly caught.

## Short-Circuiting vs. Accumulated Validation

- [**`flow {}`**]({{< relref "/reference/flow/builders-flow.md" >}}) and **`result {}`**: Ordered workflows. They stop on the first failure.
- [**`validate {}`**]({{< relref "/reference/validation/builders-validate.md" >}}): Accumulating validation. Joins errors from independent steps (using `and!`) into a structured `Diagnostics` graph.

Use the short-circuiting model when later steps depend on earlier values. Use the validation model when independent checks should all run and report back together.

## Execution Is Explicit

You run a Flow by calling [`Flow.run`]({{< relref "/reference/flow/m-flow-run.md" >}}). This returns an **`Effect<'value, 'error>`**:

- On **.NET**: `ValueTask<Exit<'value, 'error>>`.
- On **Fable**: `Async<Exit<'value, 'error>>`.

This `Effect` is the cross-platform execution boundary. It is **cold**: building a flow does not run it. Each call to `run` executes the logic from scratch.

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

Read [Getting Started](../start/getting-started/) for a tutorial-style overview, or browse the [API Reference](../../reference/) for detailed signatures.
