---
weight: 30
title: IcedTasks Integration
description: How FsFlow fits beside IcedTasks and task-oriented code.
---


# IcedTasks Integration

This page shows how FsFlow can fit beside `IcedTasks`.

`IcedTasks` is a natural neighbor when the codebase already uses task computation expressions and cold or cancellable task helpers.

FsFlow does not need to replace that layer. It can add typed failure, environment threading, and boundary management where those concerns matter.

`IcedTasks` is especially interesting when you want task-native ergonomics and performance-sensitive helpers. FsFlow stays in the picture when you want to carry a typed result and an explicit environment together with the runtime shape.

## Shared Ground

The overlap is real:

- both libraries care about task-shaped boundaries
- both libraries care about delayed execution
- both libraries care about cancellation-aware execution

The difference is the model:

- `IcedTasks` focuses on task expression ergonomics
- the task surface focuses on orchestration, typed failure, and a named environment boundary

## Why The Pair Works

- `IcedTasks` keeps the task side fast and ergonomic
- the task surface keeps the boundary model explicit with `Flow<'env, 'error, 'value>`
- the combination works well when you need task-native interop without giving up typed failures or environment/threaded context

One naming caveat matters here: `IcedTasks` uses ColdTask as an alias-style task shape, while the task surface uses a nominal `ColdTask<'value>` wrapper type. The names are similar, but the types are not interchangeable without an explicit bridge.

This page is about coexistence, not a literal side-by-side API sample from both libraries. The code below shows both sides of the boundary, with each bridge made explicit.

## How To Combine Them

Use `IcedTasks` when the codebase already centers `task {}` and its adjacent task CE helpers.

Use Flow when you want:

- a typed success/error boundary
- explicit environment threading
- ColdTask interop without losing the boundary shape

## Example

The first bridge goes from IcedTasks into FsFlow.
Use `coldTask` when you want delayed execution without a token, and `cancellableTask` when the helper needs the current `CancellationToken`:

```fsharp
open System.Threading
open System.Threading.Tasks
open FsFlow
open IcedTasks

type AppConfig =
    { Prefix: string }

let icedLoadConfig =
    coldTask {
        return { Prefix = "Hello" }
    }

let fsFlowUsesIcedTasks : Flow<unit, string, string> =
    flow {
        let! config = ColdTask.fromTaskFactory icedLoadConfig
        return config.Prefix
    }
```

`flow {}` auto-binds the `ColdTask<'value>` wrapper produced by `ColdTask.fromTaskFactory icedLoadConfig`, so the IcedTasks helper stays cold until the boundary runs. The distinction still matters: `icedLoadConfig` is an IcedTasks alias-style cold task, while `ColdTask<'value>` is a nominal wrapper type.

`coldTask` does not take a `CancellationToken`; it is just `unit -> Task<'value>`. If the IcedTasks helper needs token flow, use `cancellableTask` instead:

```fsharp
let icedLoadText path =
    cancellableTask {
        let! ct = CancellableTask.getCancellationToken ()
        return! System.IO.File.ReadAllTextAsync(path, ct)
    }
```

The second bridge goes back the other way:

```fsharp
let fsGreeting : Flow<unit, string, string> =
    flow {
        return "Hello from FsFlow"
    }

let icedTasksUsesFsFlow =
    cancellableTask {
        let! ct = CancellableTask.getCancellationToken ()
        let result = Flow.runFull () ct fsGreeting
        return result
    }
```

Use `Flow.runFull` when you want to consume an FsFlow boundary from an IcedTasks computation and pass the current cancellation token through the bridge. Use `cancellableTask` when you want the IcedTasks side to carry a token through the bridge.

## Keep Started Task Work As Task

If you already have started `Task` work, keep it as `Task` and bind it directly in `flow {}`:

```fsharp
let started = Task.FromResult 42

let taskBoundary : Flow<unit, string, int> =
    flow {
        let! value = started
        return value
    }
```

Do not wrap started work in `coldTask` just to make it look similar. Use `Task` when the work is already hot, and use `coldTask` when the helper itself should stay delayed.

## When To Prefer IcedTasks

Prefer `IcedTasks` when you mostly need:

- extra task CE variants
- cold or cancellable task helpers
- task-native ergonomics without typed orchestration

Prefer FsFlow when the boundary itself can carry the environment and typed result shape.
