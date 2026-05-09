---
weight: 20
title: Execution Semantics
description: Exact execution rules for Flow, AsyncFlow, TaskFlow, and ColdTask.
---


# Execution Semantics

This page shows the exact execution model of Flow, AsyncFlow, TaskFlow, and ColdTask.

Task-oriented semantics on this page refer to the task surface that ships in the main `FsFlow` package.
The public surface now includes sync, async, and task concepts together.
The task surface means the TaskFlow builder plus its task-aware extension members for `Task`, `ValueTask`, and ColdTask.

## Success And Typed Failure

- `Flow.succeed value` returns `Ok value`
- `AsyncFlow.succeed value` returns `Ok value`
- `TaskFlow.succeed value` returns `Ok value`
- `fail` produces the typed `Error`
- `map` only transforms successful values
- `mapError` only transforms typed failures
- `tapError` runs only on typed failure and preserves the original error when the tap succeeds
- `orElse` switches to a fallback computation when the first computation returns a typed failure

All of these semantics are short-circuiting.
The first typed failure stops the current workflow unless you explicitly recover from it.

## Short-Circuiting Versus Accumulated Validation

Check, Result, [`result {}`]({{< relref "builders-result.md" >}}), Flow, AsyncFlow, and TaskFlow model ordered workflows.
They stop on the first typed failure unless you explicitly recover from it.

Validation and [`validate {}`]({{< relref "builders-validate.md" >}}) are the accumulating path.
They merge sibling failures into the diagnostics graph instead of stopping at the first one.

Use the short-circuiting model when:

- later steps depend on earlier successful values
- the workflow should stop on the first failure
- you are orchestrating environment access, async work, tasks, retries, cancellation, or resource usage

Use the validation model when:

- independent checks should all run
- you need a structured collection of failures instead of the first failure
- the problem is sibling validation rather than workflow orchestration

## Cold By Default

All three computation families are cold.
Building a computation does not run it.

Rerun behavior:

- Flow reruns from scratch each time you call `Flow.run`
- AsyncFlow reruns from scratch each time you call `AsyncFlow.run` or `AsyncFlow.toAsync`
- TaskFlow reruns from scratch each time you call `TaskFlow.run` or `TaskFlow.toTask`

The `delay` combinator preserves that behavior in each family.

## Execution Is Explicit

Run each computation family with its own execution function:

- `Flow.run env flow`
- `AsyncFlow.toAsync env flow`
- `TaskFlow.toTask env cancellationToken flow`

The runtime shape is part of the public contract.
That is why the library keeps three families instead of forcing every computation through one wrapper.

## Exceptions

Each family exposes `catch` to convert exceptions into typed errors:

- `Flow.catch`
- `AsyncFlow.catch`
- `TaskFlow.catch`

This handles exceptions that occur while the computation is being executed.
Typed failures still stay in Result.

## Environments

Each family reads dependencies explicitly:

- `env` reads the whole environment
- `read` projects one dependency
- `localEnv` runs a smaller computation inside a larger environment

The environment semantics are aligned across all three families.

## Pairing And Small Composition

Each family also exposes low-ceremony helpers for common composition shapes:

- `zip` runs two computations in sequence and returns a tuple
- `map2` runs two computations in sequence and combines both successful values with a mapper
- both helpers short-circuit on the first typed failure

These helpers are useful when a full computation expression would add more ceremony than value.

## Task Temperature

TaskFlow and the `.NET` extensions for [`asyncFlow {}`]({{< relref "builders-asyncflow.md" >}}) distinguish between:

- already-started task values such as `Task<'value>` and `ValueTask<'value>`
- delayed task work represented by `ColdTask<'value>`

`ColdTask<'value>` means:

```fsharp
CancellationToken -> Task<'value>
```

That distinction matters because reruns behave differently:

- rerunning a computation that binds a started `Task` or `ValueTask` re-awaits the same started work
- rerunning a computation that binds a ColdTask calls the factory again

It also affects cancellation:

- a started `Task` or `ValueTask` is already running before the computation executes
- the current computation `CancellationToken` cannot be injected into that already-started work
- a ColdTask starts when the computation runs, so the current computation `CancellationToken` can be passed in

Use a hot task input only when reusing the same already-started work is the behavior you want.
Use ColdTask when the effect should start at computation execution time, rerun from scratch, or observe the runtime cancellation token.

Example with a started task:

```fsharp
let started = Task.FromResult 42

let computation : TaskFlow<unit, string, int> =
    taskFlow {
        let! value = started
        return value
    }
```

Each run re-awaits `started`.
It does not create a new task.

Example with a ColdTask:

```fsharp
let readValue : ColdTask<int> =
    ColdTask(fun cancellationToken ->
        Task.FromResult 42)

let computation : TaskFlow<unit, string, int> =
    taskFlow {
        let! value = readValue
        return value
    }
```

Each run calls the ColdTask factory again and passes in the current computation cancellation token.

## Runtime Helpers

Operational helpers for logging, timeout, retry, and resource handling are grouped into `Runtime` modules:

- **`AsyncFlow.Runtime`**: Helpers for AsyncFlow computations (cancellation, sleep, log, timeout, retry).
- **`TaskFlow.Runtime`**: Task-native helpers for TaskFlow computations, including support for `RuntimeContext`.

There is no `Flow.Runtime` because synchronous flows are intended for pure logic that does not
require operational runtime support like cancellation or timeouts.

## Validation Helpers

`FsFlow.Check` provides pure `Result<'value, unit>` checks for booleans, options, value options,
nulls, collections, equality, and strings.

Use `Check.orError` to attach a typed error after the pure validation step.

When the same source should bind directly in [`flow {}`]({{< relref "builders-flow.md" >}}), [`asyncFlow {}`]({{< relref "builders-asyncflow.md" >}}), or [`taskFlow {}`]({{< relref "taskbuilders-taskflow.md" >}}),
use `Guard.Of` for `bool`, `option`, `voption`, `Result<'value, unit>`, and
`Validation<'value, unit>` sources, or `Guard.MapError` when the source already carries an
error value.

When the error value itself needs environment or effectful evaluation, use the bridge helpers on
Flow, AsyncFlow, or TaskFlow.

Use Validation and [`validate {}`]({{< relref "builders-validate.md" >}}) when the failures should accumulate into a structured
`Diagnostics` graph instead of short-circuiting.

## Family Direction

The computation families intentionally compose upward:

- AsyncFlow can lift Flow
- TaskFlow can lift Flow
- TaskFlow can lift AsyncFlow

Keep the smallest honest computation at each boundary, then lift it only when the outer runtime actually changes.

## What The Tests Cover

The test suite currently verifies:

- sync, async, and task execution
- rerun behavior for `delay`
- direct binding across the supported wrapper shapes
- ColdTask hot and cold adaptation behavior
- cancellation-token propagation into ColdTask
- environment projection through `localEnv`
- option and value-option behavior across all builders

## Next

Read [`docs/GETTING_STARTED.md`](./GETTING_STARTED.md) for the computation-family overview,
[`docs/TASK_ASYNC_INTEROP.md`](./TASK_ASYNC_INTEROP.md) for the direct binding surface,
or [`src/FsFlow/Flow.fs`](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs) and
[`src/FsFlow/TaskFlow.fs`](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs)
for the full API surface.
