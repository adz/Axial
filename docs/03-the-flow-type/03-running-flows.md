---
title: Running Flows
description: Start a cold Flow and observe its Exit.
---

# Running Flows

Creating a Flow does not execute it. Start it explicitly at a boundary, and read the outcome from the
`Exit<'value, 'error>` it returns:

```fsharp transcript
> let workflow () : Flow<string> = Flow.succeed "Hello";;
> Flow.run () (workflow ());;
val it: Exit<string,Never> = Success "Hello"

> match Flow.run () (workflow ()) with Exit.Success value -> value | Exit.Failure cause -> Cause.prettyPrint string cause;;
val it: string = "Hello"
```

`Flow.run` blocks until the Exit is available. `Flow.startTask` starts the work at once and returns a task to await,
and `Flow.toAsync` returns an async that starts nothing until it is run:

```fsharp transcript
> let mutable started = 0;;
val started: int = 0

> let counted () : Flow<int> = Flow.delay (fun () -> started <- started + 1; Flow.ok started);;
> let cold = Flow.toAsync () (counted ()) in started;;
val it: int = 0

> (Flow.startTask () (counted ())).Result;;
val it: Exit<int,Never> = Success 1

> Flow.toAsync () (counted ()) |> Async.RunSynchronously;;
val it: Exit<int,Never> = Success 2
```

The name states when work begins. `to*` builds a description and starts nothing, `start*` begins execution
immediately and hands back a handle, and `run` executes to completion:

| Entry point | Starts work? |
| --- | --- |
| `Flow.run` / `RunSynchronously` | Yes, and blocks until the Exit is available |
| `Flow.startTask` / `StartAsTask` / `StartAsValueTask` | Yes: the work is already running when it returns |
| `Flow.toAsync` / `ToAsync` | No: nothing runs until the returned async is started |

This matters when you build a handle without awaiting it. `StartAsTask` has already begun the work at that point;
`ToAsync` has not, and discarding the async discards the work.

The members take an optional `cancellationToken` for interop callers; the module functions take none, which keeps the
common path short. Use `Flow.timeout` to bound how long a workflow may run. On Fable, use `ToAsync`.

Every call starts a fresh execution with its own root scope. Await the returned handle to receive the final Exit.

Direct execution is useful at interop boundaries. A complete application normally starts its root workflow with
`App.run`, introduced at the end of this section.

## Go Further

- [App reference](/api/) covers root application execution and lifecycle handles.
- [Exit reference](/api/) covers completed outcomes and boundary conversions.
- [Runtime operations tutorial](/platforms-and-hosting/runtime-operations.html) adds timeout, retry,
  cancellation, and annotations around an execution.
