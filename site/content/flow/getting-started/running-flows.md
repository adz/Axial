---
weight: 5
title: Running Flows
description: Start a cold Flow and observe its Exit.
type: docs
---


Creating a Flow does not execute it. Start it explicitly at a boundary:

```fsharp
let workflow = Flow.succeed "Hello"
let exit = workflow.RunSynchronously(())
```

Execution completes with `Exit<'value, 'error>`:

```fsharp
match exit with
| Exit.Success value -> printfn "%s" value
| Exit.Failure cause -> printfn "%s" (Cause.prettyPrint string cause)
```

On .NET, choose the carrier expected by the caller:

```fsharp
let valueTask = workflow.ToValueTask(())
let task = workflow.ToTask(())
let asyncWork = workflow.ToAsync(())
```

On Fable, use `ToAsync`.

Every call starts a fresh execution with its own root scope. Await the returned handle to receive the final Exit.

Direct execution is useful at interop boundaries. A complete application normally starts its root workflow with
`App.run`, introduced at the end of this section.

## Go Further

- [App reference]({{< relref "/flow/reference/app/" >}}) covers root application execution and lifecycle handles.
- [Exit reference]({{< relref "/flow/reference/exit/" >}}) covers completed outcomes and boundary conversions.
- [Runtime operations tutorial]({{< relref "/flow/tutorials/runtime-operations/" >}}) adds timeout, retry,
  cancellation, and annotations around an execution.
