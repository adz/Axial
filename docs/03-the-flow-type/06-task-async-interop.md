---
title: Async and Task interop
description: Convert .NET asynchronous work into cold, cancellable Flows without losing the typed error channel.
---

# Async and Task interop

Axial distinguishes a description of asynchronous work from work that has already started. Use that distinction to keep
workflows rerunnable, pass cancellation to external operations, and preserve `Result.Error` in Flow's typed error
channel.

## Choose an interop form

| Source | Use | Behavior |
| --- | --- | --- |
| `Async<'value>` | Bind directly in `flow { }` | Cold and rerunnable; the value is successful output |
| `Async<Result<'value,'error>>` | Bind directly in `flow { }` | Cold and rerunnable; `Error` enters the typed error channel |
| `ColdTask<'value>` | Bind directly in `flow { }` | Cold task factory; receives Flow's cancellation token |
| `ColdTask<Result<'value,'error>>` | Bind directly in `flow { }` | Cold task factory; `Error` enters the typed error channel |
| `CancellationToken -> Task<'value>` | `Flow.fromTask` | Creates a cold Flow whose task value is successful output |
| `CancellationToken -> Task<Result<'value,'error>>` | `Flow.fromTaskResult` | Creates a cold Flow whose `Error` enters the typed error channel |
| Already-running `Task<'value>` | `Flow.awaitStartedTask` | Awaits the existing operation; Flow cannot pass cancellation into it |
| Already-running `Task<Result<'value,'error>>` | `Flow.awaitStartedTaskResult` | Awaits the existing operation and lifts `Error` |

`ValueTask` has the corresponding `fromValueTask`, `fromValueTaskResult`, `awaitStartedValueTask`, and
`awaitStartedValueTaskResult` functions.

## Bind Async values

`Async` is already a cold F# computation. Bind it directly:

```fsharp no-check reason="Application-specific asynchronous APIs and domain types are described in the surrounding prose"
let loadCount : Async<int> =
    async { return 42 }

let workflow =
    flow {
        let! count = loadCount
        return count + 1
    }
```

An outer `Result` is part of the Flow contract, not a nested success value:

```fsharp no-check reason="Application-specific asynchronous APIs and domain types are described in the surrounding prose"
let loadUser : Async<Result<User, LoadUserError>> =
    repository.loadUser userId

let workflow : Flow<unit, LoadUserError, string> =
    flow {
        let! user = loadUser
        return user.Name
    }
```

The same lifting applies to `return!`:

```fsharp no-check reason="Application-specific asynchronous APIs and domain types are described in the surrounding prose"
let workflow : Flow<unit, LoadUserError, User> =
    flow {
        return! loadUser
    }
```

Use `Flow.fromAsync` or `Flow.fromAsyncResult` when composing without `flow { }`.

## Bind cold Task work

A `Task` starts when the method that returns it runs. Wrap the factory in `ColdTask` so the method runs only when the
Flow runs:

```fsharp no-check reason="Application-specific asynchronous APIs and domain types are described in the surrounding prose"
let loadUser : ColdTask<Result<User, LoadUserError>> =
    ColdTask(fun cancellationToken ->
        repository.loadUserAsync(userId, cancellationToken))

let workflow : Flow<unit, LoadUserError, string> =
    flow {
        let! user = loadUser
        return user.Name
    }
```

`ColdTask<Result<_,_>>` lifts its outer `Result` for both `let!` and `return!`:

```fsharp no-check reason="Application-specific asynchronous APIs and domain types are described in the surrounding prose"
let workflow : Flow<unit, LoadUserError, User> =
    flow {
        return! loadUser
    }
```

Each execution invokes the factory again and supplies that execution's cancellation token. Retry, repeat, timeout, race,
and interruption therefore operate on newly started work.

## Convert a Task factory without a builder

Use `Flow.fromTask` when the task returns an ordinary value:

```fsharp no-check reason="Application-specific asynchronous APIs and domain types are described in the surrounding prose"
let download : Flow<unit, Never, byte array> =
    Flow.fromTask(fun cancellationToken ->
        client.GetByteArrayAsync(uri, cancellationToken))
```

Use `Flow.fromTaskResult` when the task returns an expected application failure:

```fsharp no-check reason="Application-specific asynchronous APIs and domain types are described in the surrounding prose"
let loadUser : Flow<unit, LoadUserError, User> =
    Flow.fromTaskResult(fun cancellationToken ->
        repository.loadUserAsync(userId, cancellationToken))
```

Both functions invoke their factory on every execution. Thrown exceptions are defects; cancellation is interruption.
The `Result` variant changes only how the returned value is interpreted.

## Await work that already started

Sometimes an API gives you a Task that is already running:

```fsharp no-check reason="Application-specific asynchronous APIs and domain types are described in the surrounding prose"
let runningTask = repository.beginRefresh()

let refresh : Flow<unit, Never, RefreshSummary> =
    Flow.awaitStartedTask runningTask
```

If it returns `Result`, use the typed-error form:

```fsharp no-check reason="Application-specific asynchronous APIs and domain types are described in the surrounding prose"
let runningTask : Task<Result<RefreshSummary, RefreshError>> =
    repository.beginRefresh()

let refresh : Flow<unit, RefreshError, RefreshSummary> =
    Flow.awaitStartedTaskResult runningTask
```

An already-running task has different lifecycle semantics:

- It started before the Flow.
- Reusing the Flow awaits the same operation.
- Flow cannot inject its cancellation token into work that already started.
- Prefer a cold factory when you control task creation.

Raw `Task` and `ValueTask` values do not bind directly in `flow { }`. This prevents an already-running operation from
looking like a cold workflow description.

## Handle expected exceptions

The `from*`, `ColdTask`, and `awaitStarted*` paths treat thrown exceptions as defects. Use an `attempt*` function when
an exception is an expected failure that callers should handle:

```fsharp no-check reason="Application-specific asynchronous APIs and domain types are described in the surrounding prose"
let read : Flow<unit, exn, string> =
    Flow.attemptTask(fun cancellationToken ->
        File.ReadAllTextAsync(path, cancellationToken))
```

Available functions include:

```fsharp no-check reason="Application-specific asynchronous APIs and domain types are described in the surrounding prose"
Flow.attemptAsync
Flow.attemptTask
Flow.attemptValueTask
Flow.attemptStartedTask
Flow.attemptStartedValueTask
```

`OperationCanceledException` and `TaskCanceledException` become interruption rather than `Cause.Fail exn`.

## Keep a Result as the successful value

The builder interprets one outer `Result` as Flow's error channel. Add another successful layer when a nested Result is
the value you intentionally need:

```fsharp no-check reason="Application-specific asynchronous APIs and domain types are described in the surrounding prose"
let inspect : ColdTask<Result<Result<User, LoadUserError>, Never>> =
    ColdTask(fun cancellationToken ->
        task {
            let! result = repository.loadUserAsync(userId, cancellationToken)
            return (Ok result : Result<_, Never>)
        })

let workflow : Flow<unit, Never, Result<User, LoadUserError>> =
    flow {
        return! inspect
    }
```

The builder lifts the outer `Result<_,Never>` and leaves the inner `Result<User,LoadUserError>` as the successful value.

## Summary

- Bind `Async` and `ColdTask` directly in `flow { }`.
- An outer `Result.Error` always enters Flow's typed error channel.
- Use `ColdTask` or `Flow.fromTask*` to start task work when the Flow runs.
- Use `Flow.awaitStarted*` only for work that has already started.
- Raw `Task` and `ValueTask` values are not Flow builder sources.
- Use `attempt*` when exceptions are expected failures rather than defects.
