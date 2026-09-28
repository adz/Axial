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

## The examples on this page

The examples use a stand-in for a data-access class. Its `LoadUserAsync` method starts work as soon as it is called,
like most .NET APIs that return a `Task`, and it counts how often it was called:

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Layers
open Axial.Console
open Axial.FileSystem
open Axial.Hosting
open Axial.Hosting.Browser
open Axial.Hosting.Node
open Axial.PlatformService
open Axial.State
open Axial.Telemetry
open Axial.Telemetry.JavaScript

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
type User = { Id: int; Name: string }
type LoadUserError = UserNotFound of int

type UserRepository() =
    let mutable calls = 0
    member _.Calls = calls

    member _.LoadUserAsync(id: int, cancellationToken: CancellationToken) : Task<Result<User, LoadUserError>> =
        calls <- calls + 1

        task {
            do! Task.Delay(1, cancellationToken)
            return if id = 1 then Ok { Id = 1; Name = "Ada" } else Error(UserNotFound id)
        }

    member this.LoadUser(id: int) : Async<Result<User, LoadUserError>> =
        async {
            let! cancellationToken = Async.CancellationToken
            return! this.LoadUserAsync(id, cancellationToken) |> Async.AwaitTask
        }
```

## Bind Async values

`Async` is already a cold F# computation. Bind it directly:

```fsharp
let loadCount : Async<int> = async { return 42 }

let plusOne : Flow<int> =
    flow {
        let! count = loadCount
        return count + 1
    }
```

An outer `Result` is part of the Flow contract, not a nested success value: `Ok` continues the block and `Error` enters
the typed error channel. The same applies to `return!`:

```fsharp
let repository = UserRepository()

let userName (id: int) : Flow<LoadUserError, string> =
    flow {
        let! user = repository.LoadUser id
        return user.Name
    }

let loadUser (id: int) : Flow<LoadUserError, User> = flow { return! repository.LoadUser id }
```

```fsharp run
plusOne |> Flow.run () |> shouldEqual (Exit.Success 43)
userName 1 |> Flow.run () |> shouldEqual (Exit.Success "Ada")
userName 2 |> Flow.run () |> shouldEqual (Exit.Failure(Cause.Fail(UserNotFound 2)))
loadUser 1 |> Flow.run () |> shouldEqual (Exit.Success { Id = 1; Name = "Ada" })
```

Use `Flow.fromAsync` or `Flow.fromAsyncResult` when composing without `flow { }`.

## Bind cold Task work

A `Task` starts when the method that returns it runs. Wrap the call in `ColdTask` so the method runs only when the Flow
runs. `ColdTask<Result<_,_>>` lifts its outer `Result` for both `let!` and `return!`:

```fsharp
let loadUserCold (id: int) : ColdTask<Result<User, LoadUserError>> =
    ColdTask(fun cancellationToken -> repository.LoadUserAsync(id, cancellationToken))

let coldName (id: int) : Flow<LoadUserError, string> =
    flow {
        let! user = loadUserCold id
        return user.Name
    }
```

Building the flow calls nothing. Each execution calls the method again and passes that execution's cancellation token,
so retry, repeat, timeout, race, and interruption operate on newly started work:

```fsharp run
let before = repository.Calls
let described = coldName 1
repository.Calls |> shouldEqual before

described |> Flow.run () |> shouldEqual (Exit.Success "Ada")
described |> Flow.run () |> shouldEqual (Exit.Success "Ada")
repository.Calls |> shouldEqual (before + 2)
```

## Convert a Task factory without a builder

`Flow.fromTask` takes a function from the cancellation token to a task that returns an ordinary value.
`Flow.fromTaskResult` takes one whose task returns a `Result`, and sends `Error` to the typed error channel:

```fsharp
let measure (text: string) : Flow<int> =
    Flow.fromTask (fun cancellationToken ->
        task {
            do! Task.Delay(1, cancellationToken)
            return text.Length
        })

let loadFirstUser : Flow<LoadUserError, User> =
    Flow.fromTaskResult (fun cancellationToken -> repository.LoadUserAsync(1, cancellationToken))
```

```fsharp run
measure "four" |> Flow.run () |> shouldEqual (Exit.Success 4)
loadFirstUser |> Flow.run () |> shouldEqual (Exit.Success { Id = 1; Name = "Ada" })
```

Both functions call their factory on every execution. Thrown exceptions are defects; cancellation is interruption.

## Await work that already started

Sometimes an API gives you a Task that is already running. `Flow.awaitStartedTask` awaits it, and
`Flow.awaitStartedTaskResult` also lifts an `Error`:

```fsharp run
let callsBefore = repository.Calls
let runningTask = repository.LoadUserAsync(1, CancellationToken.None)
let refresh : Flow<LoadUserError, User> = Flow.awaitStartedTaskResult runningTask

refresh |> Flow.run () |> shouldEqual (Exit.Success { Id = 1; Name = "Ada" })
refresh |> Flow.run () |> shouldEqual (Exit.Success { Id = 1; Name = "Ada" })
repository.Calls |> shouldEqual (callsBefore + 1)
```

The repository was called once, when `runningTask` was created. An already-running task has different lifecycle
semantics:

- It started before the Flow.
- Reusing the Flow awaits the same operation.
- Flow cannot pass its cancellation token into work that already started.
- Prefer a cold factory when you control task creation.

Raw `Task` and `ValueTask` values do not bind directly in `flow { }`. This prevents an already-running operation from
looking like a cold workflow description.

## Handle expected exceptions

The `from*`, `ColdTask`, and `awaitStarted*` paths treat thrown exceptions as defects. Use an `attempt*` function when
an exception is an expected failure that callers should handle:

```fsharp
let readFile (path: string) : ExnFlow<string> =
    Flow.attemptTask (fun cancellationToken -> File.ReadAllTextAsync(path, cancellationToken))

let failureName (exit: Exit<'value, exn>) =
    match exit with
    | Exit.Failure(Cause.Fail error) -> error.GetType().Name
    | _ -> "no typed failure"
```

```fsharp run
readFile "/no/such/file.txt" |> Flow.run () |> failureName |> shouldEqual "DirectoryNotFoundException"
```

Available functions include:

```fsharp
Flow.attemptAsync
Flow.attemptTask
Flow.attemptValueTask
Flow.attemptStartedTask
Flow.attemptStartedValueTask
```

`OperationCanceledException` and `TaskCanceledException` become interruption when the runtime's cancellation token
requested them. Cancellation that the operation raised for its own reasons, such as a library's internal timeout, is a
failure like any other exception: `Cause.Fail exn` from `attempt*`, a defect from `from*`.

## Wrap blocking calls

Some libraries only offer synchronous, blocking calls: database drivers, LibGit2Sharp, image codecs. Wrap them with
`Flow.fromBlocking` so the call runs on the thread pool instead of stalling the workflow's thread:

```fsharp
let slowLookup (key: string) : Flow<string> =
    Flow.fromBlocking (fun _ ->
        Thread.Sleep 5 // stands in for a synchronous driver call
        key.ToUpperInvariant())
```

```fsharp run
slowLookup "axial" |> Flow.run () |> shouldEqual (Exit.Success "AXIAL")
```

Once started, blocking work runs to completion even if the workflow is interrupted, because it cannot be abandoned
safely. The operation receives the runtime's cancellation token, so a call that can observe it stops early.
`Flow.fromBlockingResult` sends an `Error` to the typed error channel, and `Flow.attemptBlocking` treats thrown
exceptions as `Cause.Fail exn`. On JavaScript the operation runs inline.

## Keep a Result as the successful value

The builder interprets one outer `Result` as Flow's error channel. Add another successful layer when a nested Result is
the value you intentionally need:

```fsharp
let inspect (id: int) : ColdTask<Result<Result<User, LoadUserError>, Never>> =
    ColdTask(fun cancellationToken ->
        task {
            let! result = repository.LoadUserAsync(id, cancellationToken)
            return (Ok result: Result<_, Never>)
        })

let lookup (id: int) : Flow<Result<User, LoadUserError>> = flow { return! inspect id }
```

```fsharp run
lookup 2 |> Flow.run () |> shouldEqual (Exit.Success(Error(UserNotFound 2)))
```

The builder lifts the outer `Result<_,Never>` and leaves the inner `Result<User,LoadUserError>` as the successful value.

## Summary

- Bind `Async` and `ColdTask` directly in `flow { }`.
- An outer `Result.Error` always enters Flow's typed error channel.
- Use `ColdTask` or `Flow.fromTask*` to start task work when the Flow runs.
- Use `Flow.awaitStarted*` only for work that has already started.
- Raw `Task` and `ValueTask` values are not Flow builder sources.
- Use `attempt*` when exceptions are expected failures rather than defects.
- Use `Flow.fromBlocking*` for synchronous library calls that block.
