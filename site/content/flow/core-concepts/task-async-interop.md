---
weight: 50
title: Task and Async Interop
description: Direct binding rules for async and task work in Axial.
type: docs
---


Use `flow {}` for flows, results, F# async work, and .NET tasks. The same block can use all of them.

## Direct Binds

`let!` binds the completed value to the name on its left. `do!` binds work returning `unit`.
`return!` uses another flow as the result of the block.

```fsharp
flow {
    let! user = fetchUser userId
    do! saveUser user
    return! notifyUser user
}
```

Here is the same block with the left- and right-hand types shown:

```fsharp
flow {
    let! (user: User) = (fetchUser userId: Task<User>)
    do! (saveUser user: Async<unit>)
    return! (notifyUser user: Flow<AppEnv, AppError, Receipt>)
}
// Flow<AppEnv, AppError, Receipt>
```

| Type | Outcome |
| :--- | :--- |
| `Flow<'env, 'error, 'value>` | Continues with the flow's value. |
| `Result<'value, 'error>` | Continues on `Ok`, short-circuits on `Error`. |
| `Async<'value>` | Awaits the async and continues with the value. |
| `Async<Result<'value, 'error>>` | Awaits the async and handles the Result outcome. |
| `Task<'value>` | Awaits the task and continues with the value. |
| `Task<Result<'value, 'error>>` | Awaits the task and handles the Result outcome. |
| `ValueTask<'value>` | Awaits the value task and continues with the value. |
| `ValueTask<Result<'value, 'error>>` | Awaits and handles the Result outcome. |

Direct `Async`, `Task`, and `ValueTask` binds treat thrown exceptions as defects (`Cause.Die`) and cancellation as interruption (`Cause.Interrupt`). Use the attempt constructors when exceptions are expected and should become typed failures:

```fsharp
let loadFromInterop : ExnFlow<string> =
    Flow.attemptTask (legacyClient.LoadAsync())
```

`Flow.attemptAsync`, `Flow.attemptTask`, and `Flow.attemptValueTask` return `Cause.Fail exn` for non-cancellation exceptions. `Flow.attemptTask` and `Flow.attemptValueTask` are .NET only.

### Example: Mixed Orchestration

```fsharp
let fetchUser (id: int) : Task<User> = ...
let validate (user: User) : Result<User, string> = ...
let saveUser (user: User) : Async<unit> = ...

let processUser id =
    flow {
        // Bind a .NET Task
        let! user = fetchUser id
        
        // Bind a Result
        let! validUser = validate user
        
        // Bind an F# Async
        do! saveUser validUser
        
        return "Done"
    }
```

## Option and ValueOption

`Option<'value>` and `ValueOption<'value>` can also be bound directly, but only if the flow's error type is `unit`.

```fsharp
let maybeValue = Some 42

let workflow : Flow<unit, int> =
    flow {
        let! x = maybeValue // Binds directly because error is unit
        return x
    }
```

If you need a specific error when an option is `None`, use `Flow.fromOption`:

```fsharp
let workflow : Flow<unit, string, int> =
    flow {
        let! x = maybeValue |> Flow.fromOption "Value was missing"
        return x
    }
```

## Hot vs. Cold Work

Understanding the difference between "Hot" and "Cold" work is crucial for correct execution and cancellation behavior.

### Hot Work (Started Tasks)
Types like `Task<'T>` and `ValueTask<'T>` are **Hot**. The work might already be running before you bind it. 
- Rerunning the flow re-awaits the same underlying work.
- You cannot pass the flow's runtime `CancellationToken` into work that has already started.

### Cold Work (Flows and ColdTask)
`Flow` itself and the `ColdTask<'T>` type are **Cold**. The work only starts when the flow is executed by `ToTask`, `ToAsync`, `ToValueTask`, or `RunSynchronously`.
- Rerunning the flow repeats the work from scratch.
- The runtime `CancellationToken` is automatically passed into the work.

### Using `ColdTask<'T>`
`ColdTask<'T>` is a simple wrapper: `CancellationToken -> Task<'T>`. It allows you to define task-based work that remains lazy and cancellation-aware.

```fsharp
let loadData path = 
    ColdTask(fun ct -> File.ReadAllTextAsync(path, ct))

let myFlow =
    flow {
        let! text = loadData "info.txt"
        return text
    }
```

### Pitfall: Don't Register a Second Cancellation Observer on the Same Token

`Flow.zipPar`, `Flow.race`, and `Flow.Runtime.timeout` interrupt a branch by cancelling the
`CancellationToken` your adapter was handed. If your adapter already awaits a cancellation-aware
operation on that token (`Task.Delay(ms, ct)`, an `HttpClient` call, `File.ReadAllTextAsync(path, ct)`,
etc.), that is sufficient — the awaited call will throw `OperationCanceledException` /
`TaskCanceledException` when interrupted, and `flow {}` turns that into `Cause.Interrupt` for you.

Do not *also* call `ct.Register(callback)` to observe the same cancellation as a side channel:

```fsharp
// Don't do this — two observers racing on one token.
task {
    use _ = ct.Register(fun () -> sideEffect ())   // manual observer #1
    do! Task.Delay(30_000, ct)                     // observer #2, built into Task.Delay
    return result
}
```

`CancellationTokenSource.Cancel()` runs every registered callback synchronously, in unspecified
order, on whichever thread called `Cancel()`. If the framework's own registration (the one behind
`Task.Delay`) happens to run first, its continuation can execute *reentrantly*, inside that same
`Cancel()` call — and if that continuation disposes your registration (e.g. a `use` binding going
out of scope in a `finally`) before your callback has run, your callback is silently skipped, not
delayed. This isn't a race that resolves given enough time; it's a real chance the side effect
never happens, and it reproduces more often under contention (many concurrent fibers, CI runners
under load), which is exactly the kind of environment that makes it feel like "just flakiness."

Instead, observe cancellation from the operation you're already awaiting:

```fsharp
task {
    try
        do! Task.Delay(30_000, ct)
        return result
    with :? OperationCanceledException ->
        sideEffect ()
        return fallback
}
```

This applies to any adapter written against a `Flow`-supplied token, not just tests — anywhere you
combine a manual `Register` with another cancellation-aware call on the same token is at risk.

## Bind: Bridging with Error Packaging

When a source needs its error assigned or mapped before `flow {}` binds it, use **`Bind`** at the binding site.

```fsharp
let myFlow =
    flow {
        let! value =
            Task.FromResult(None)
            |> Bind.error "missing"

        return value
    }
```

## Summary

- Use **`flow {}`** for all application orchestration.
- Prefer **direct binding** for `Async`, `Task`, and `Result`.
- Use **`ColdTask`** for task-based logic that should respect flow cancellation, retry, and repetition.
- Use **`Bind`** when a bind source needs `error` or `mapError` before entering `flow {}`.
