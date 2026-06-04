---
weight: 20
title: Execution and Outcomes
description: How to start flows and handle Exit values.
---

# Execution and Outcomes

`Flow<'env, 'error, 'value>` is cold. Building a workflow does not run it.

Execution starts only at an explicit boundary:

```fsharp
let workflow = Flow.succeed "Hello"

let exit = workflow.RunSynchronously(())
```

On .NET you can choose the platform handle:

```fsharp
let valueTask = workflow.ToValueTask(env)
let task = workflow.ToTask(env)
let asyncWork = workflow.ToAsync(env)
```

On Fable, use:

```fsharp
let asyncWork = workflow.ToAsync(env)
```

`ToValueTask`, `ToTask`, and `ToAsync` start the workflow and return a platform handle. They do not synchronously wait
for completion. Await or run the returned handle to observe the final `Exit<'value, 'error>`.

## Exit

Execution completes with `Exit<'value, 'error>`:

```fsharp
match exit with
| Exit.Success value ->
    printfn "Succeeded: %A" value
| Exit.Failure cause ->
    printfn "Failed: %s" (Cause.prettyPrint string cause)
```

`Cause<'error>` distinguishes typed failures from defects and interruption:

| Cause | Meaning |
| :--- | :--- |
| `Fail error` | Expected domain failure. |
| `Die exn` | Unexpected defect. |
| `Interrupt` | Cooperative cancellation/interruption. |
| `Then (first, second)` | Ordered failure composition. |
| `Both (left, right)` | Parallel failure composition. |
| `Traced (cause, trace)` | Diagnostic annotation. |

## Converting to Result

Use `Exit.toResult` only at boundaries that need standard F# `Result`:

```fsharp
task {
    let! exit = workflow.ToTask(env)
    return Exit.toResult exit
}
```

`Exit.toResult` raises for defects, interruption, and composite causes because `Result` cannot represent those outcomes
without losing information.
