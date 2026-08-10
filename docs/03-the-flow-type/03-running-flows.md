---
title: Running Flows
description: Start a cold Flow and observe its Exit.
---

# Running Flows

Creating a Flow does not execute it. Start it explicitly at a boundary:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let workflow = Flow.succeed "Hello"
let exit = workflow |> Flow.run ()
```

Execution completes with `Exit<'value, 'error>`:

```fsharp no-check reason="Shown independently; surrounding application context is intentionally omitted"
match exit with
| Exit.Success value -> printfn "%s" value
| Exit.Failure cause -> printfn "%s" (Cause.prettyPrint string cause)
```

In a pipeline, use the module functions:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let exit = workflow |> Flow.run ()               // executes, blocks
let running = workflow |> Flow.startTask ()      // executes now, returns a handle
let cold = workflow |> Flow.toAsync ()           // executes nothing yet
```

The name states when work begins. `to*` builds a description and starts nothing, `start*` begins execution
immediately and hands back a handle, and `run` executes to completion:

| Entry point | Starts work? |
| --- | --- |
| `Flow.run` / `RunSynchronously` | Yes, and blocks until the Exit is available |
| `Flow.startTask` / `StartAsTask` / `StartAsValueTask` | Yes — the work is already in flight when it returns |
| `Flow.toAsync` / `ToAsync` | No — nothing runs until the returned async is started |

This matters when you build a handle without awaiting it. `StartAsTask` has already begun the work at that point;
`ToAsync` has not, and discarding the async discards the work.

The members carry optional `cancellationToken` and `timeout` arguments for interop callers; the module functions
take none, which keeps the common path short. On Fable, use `ToAsync`.

Every call starts a fresh execution with its own root scope. Await the returned handle to receive the final Exit.

Direct execution is useful at interop boundaries. A complete application normally starts its root workflow with
`App.run`, introduced at the end of this section.

## Go Further

- [App reference](/api.html) covers root application execution and lifecycle handles.
- [Exit reference](/api.html) covers completed outcomes and boundary conversions.
- [Runtime operations tutorial](/platforms-and-hosting/runtime-operations.html) adds timeout, retry,
  cancellation, and annotations around an execution.
