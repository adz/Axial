---
weight: 20
title: Execution Semantics
description: Exact execution rules for Flow.
type: docs
---


`Flow<'env, 'error, 'value>` is a cold workflow description. It reads an explicit environment, can fail with typed
errors, and produces an `Exit<'value, 'error>` when executed.

Conceptually:

```text
Flow -> platform execution handle -> Exit
```

The platform handle is chosen at the execution boundary:

- `.NET`: `ToValueTask`, `ToTask`, `ToAsync`, or `RunSynchronously`.
- `Fable`: `ToAsync`.

## Execution Is Explicit

```fsharp
let exit = workflow.RunSynchronously(env)

task {
    let! exit = workflow.ToTask(env)
    return exit
}

async {
    let! exit = workflow.ToAsync(env)
    return exit
}
```

`ToValueTask`, `ToTask`, and `ToAsync` start the workflow and return a platform handle. Await or run that handle to
observe the `Exit`. `RunSynchronously` starts the workflow and blocks until the `Exit` is available.

Each execution starts from scratch with a fresh root scope.

## Success and Failure

Every execution results in:

- `Exit.Success value`: the workflow completed successfully.
- `Exit.Failure (Cause.Fail error)`: expected domain failure.
- `Exit.Failure Cause.Interrupt`: cooperative interruption or cancellation.
- `Exit.Failure (Cause.Die exception)`: unexpected defect.
- `Exit.Failure (Cause.Then (first, second))`: sequential failure composition.
- `Exit.Failure (Cause.Both (left, right))`: parallel failure composition.
- `Exit.Failure (Cause.Traced (cause, trace))`: diagnostic annotation.

All standard flow combinators are short-circuiting unless a specific API says otherwise.

## Result Conversion

`Exit.toResult` is intentionally lossy and should be used only at interop boundaries. It returns `Error error` for a
simple `Cause.Fail error`, and raises for defects, interruption, and composite causes.

## Interruption and Scope

Cancellation tokens supplied to execution members are visible through `Flow.Runtime.cancellationToken`. Forked fibers
and scoped resources are tied to the current runtime scope. When the root execution ends, Axial closes the root scope
and runs registered finalizers.

If both workflow execution and cleanup fail, Axial preserves both causes with `Cause.Then`.
