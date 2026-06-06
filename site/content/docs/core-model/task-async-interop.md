---
weight: 30
title: Task and Async Interop
description: Direct binding rules for async and task work in FsFlow.
type: docs
---


FsFlow provides a single, unified computation expression—**`flow {}`**—that handles synchronous code, F# `Async`, and .NET `Task` interop natively. You don't have to choose between different builders; the same flow can orchestrate all these effect types.

## Direct Binds

Inside a `flow {}` block, you can use `let!` to bind many common F# and .NET types directly. FsFlow handles the conversion to its internal execution model automatically.

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

let workflow : Flow<unit, unit, int> =
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

## BindError: Bridging with Error Packaging

When a source needs its error assigned or mapped before `flow {}` binds it, use **`BindError`** at the binding site.

```fsharp
let myFlow =
    flow {
        let! value =
            Task.FromResult(None)
            |> BindError.withError "missing"

        return value
    }
```

## Summary

- Use **`flow {}`** for all application orchestration.
- Prefer **direct binding** for `Async`, `Task`, and `Result`.
- Use **`ColdTask`** for task-based logic that should respect flow cancellation, retry, and repetition.
- Use **`BindError`** when a bind source needs `withError` or `map` before entering `flow {}`.
