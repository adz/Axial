---
weight: 30
title: Common Shapes
description: Small examples for the main FsFlow computation families.
---


# Common Shapes

This page shows the smallest useful examples for each FsFlow computation family without the larger application setup from the main guides.

These examples are intentionally small.
They are the quickest way to sanity-check the model before moving on to the longer docs.

## Plain Result In [`flow {}`]({{< relref "builders-flow.md" >}})

```fsharp
let validatePort value =
    if value > 0 then Ok value else Error "port must be positive"

let computation : Flow<unit, string, int> =
    flow {
        let! port = validatePort 8080
        return port
    }
```

Run it with:

```fsharp
let result = computation |> Flow.run ()
```

## Read A Dependency From The Environment

```fsharp
type AppEnv = { Prefix: string }

let computation : Flow<AppEnv, string, string> =
    flow {
        let! prefix = Flow.read _.Prefix
        return $"{prefix} world"
    }
```

## Lift Sync Work Into [`asyncFlow {}`]({{< relref "builders-asyncflow.md" >}})

```fsharp
let validateName name =
    if System.String.IsNullOrWhiteSpace name then Error "missing"
    else Ok name

let computation : AsyncFlow<unit, string, string> =
    asyncFlow {
        let! name = validateName "Ada"
        let! suffix = async { return "!" }
        return name + suffix
    }
```

Run it with:

```fsharp
let result =
    computation
    |> AsyncFlow.toAsync ()
    |> Async.RunSynchronously
```

## Bind A Task In [`taskFlow {}`]({{< relref "taskbuilders-taskflow.md" >}})

```fsharp
let computation : TaskFlow<unit, string, int> =
    taskFlow {
        let! value = Task.FromResult 42
        return value
    }
```

Run it with:

```fsharp
let result =
    computation
    |> TaskFlow.toTask () CancellationToken.None
    |> Async.AwaitTask
    |> Async.RunSynchronously
```

Use a direct `Task` bind when you already have started work and reusing that same work on rerun is acceptable.

## Bind A ColdTask

```fsharp
let readText path : ColdTask<string> =
    ColdTask(fun ct -> System.IO.File.ReadAllTextAsync(path, ct))

let computation : TaskFlow<unit, string, string> =
    taskFlow {
        let! text = readText "config.json"
        return text
    }
```

Use `ColdTask<'value>` when work should start only when the task-oriented computation runs, should rerun from scratch on each execution, or should observe the computation cancellation token.

## Combine Two Small Flows

```fsharp
let combined : Flow<int, string, int> =
    Flow.map2 (+)
        (Flow.read (fun env -> env + 1))
        (Flow.read (fun env -> env * 2))
```

Use `zip` when you want both values as a tuple, or `map2` when you want to combine them immediately without opening a computation expression.

## Next

Read [`docs/GETTING_STARTED.md`](./GETTING_STARTED.md) for the full computation-family overview,
[`docs/INTEGRATIONS.md`](./INTEGRATIONS.md) if you are adopting the library incrementally,
and [`docs/TASK_ASYNC_INTEROP.md`](./TASK_ASYNC_INTEROP.md) for the direct binding surface.
