---
weight: 20
title: Execution Semantics
description: Exact execution rules for Flow.
---

# Execution Semantics

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
- `Exit.Failure (Cause.Die exception)`: An unexpected defect or crash. `Flow.run`, `Flow.runFull`, and `Flow.runSync` preserve uncaught exceptions in this branch.
- `Exit.Failure (Cause.Then (first, second))`: Two failures happened in sequence, usually a workflow failure followed by a finalizer failure.
- `Exit.Failure (Cause.Both (left, right))`: Two failures happened concurrently, usually from parallel branches or parallel layer provisioning.
- `Exit.Failure (Cause.Traced (cause, trace))`: A failure cause annotated with diagnostic text.

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

## Success and Failure Causes

FsFlow distinguishes between expected failures, administrative signals, unexpected defects, sequential composition, parallel composition, and diagnostic traces. For a detailed explanation of the architectural rationale behind this split, see [**Defects and Exceptions**]({{< relref "defects.md" >}}).

`Cause<'error>` is a tree, not only a single leaf:

- `Cause.Fail error`: Expected typed failure.
- `Cause.Die exn`: Unexpected defect preserved as an exception value.
- `Cause.Interrupt`: Cooperative cancellation/interruption.
- `Cause.Then (first, second)`: Ordered failure composition. FsFlow uses this when cleanup fails after the original workflow already failed.
- `Cause.Both (left, right)`: Parallel failure composition. FsFlow uses this when two independent branches fail before either one can be treated as only the loser of cancellation.
- `Cause.Traced (cause, trace)`: Diagnostic annotation around another cause.

Use `Cause.failures`, `Cause.defects`, `Cause.isInterrupted`, and `Cause.prettyPrint` at host or logging boundaries when you need a flattened view. Inside FsFlow, prefer preserving the full cause tree.

`Exit.toResult` is intentionally lossy because `Result<'value, 'error>` can represent only success or one typed error. It returns `Error error` only for a simple `Cause.Fail error`. It raises for defects, interruption, and composite causes rather than silently discarding cause structure.

## Interruption and Cancellation

FsFlow supports algebraic interruption. When a [**Fiber**]({{< relref "fibers.md" >}}) is interrupted (e.g., via `Flow.interrupt` or a `CancellationToken` trigger), the flow stops executing and returns `Exit.Failure Cause.Interrupt`.

Important interruption rules:

- A thrown `OperationCanceledException` normally becomes `Cause.Interrupt`.
- `Flow.Runtime.timeout` returns the timeout error you supplied as `Cause.Fail error`.
- `Flow.interrupt fiber` asks the child to stop and returns the child's final `Exit`.
- `Flow.race` returns the winner's outcome and interrupts the loser; the loser interruption is not part of the winner's outcome.
- `Flow.zipPar` interrupts the other branch after the first failure. If the other branch reports only that synthetic interruption, the visible result is the original failure. If both branches independently fail, the visible result is `Cause.Both`.
- Runtime and layer scopes always try to run finalizers. If the main workflow fails and cleanup also fails, the result is `Cause.Then (workflowCause, cleanupCause)`.

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

Operational helpers for logging, timeout, retry, annotations, and resource handling are grouped into the `Flow.Runtime` and `Schedule` modules.

Use `Flow.annotate` and `Flow.traceId` to add runtime metadata around a flow:

```fsharp
let workflow =
    flow {
        let! traceId = Flow.Runtime.traceId
        return traceId
    }
    |> Flow.annotate "deviceId" "device-1"
    |> Flow.traceId "trace-123"
```

Annotations are scoped to the wrapped flow. Nested annotations with the same key override the outer value only while the nested flow runs.
