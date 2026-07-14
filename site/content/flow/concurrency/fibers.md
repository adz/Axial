---
weight: 10
title: Fibers
description: Lightweight logical threads and structured concurrency in Axial.
type: docs
---


Fibers represent running child workflows.

In Axial, a **Fiber** is a handle to a running [**`Flow`**]({{< relref "/flow/reference/flow/t-flow.md" >}}). A flow is a cold description of work. A fiber is the hot execution that exists after that work has been started in the background.

## The Mental Model

While a `Flow` is **cold** (a description of work that hasn't started yet), a **Fiber** is **hot** (the work is currently being executed).

When you fork a flow, you are saying: start this work now, give me a typed handle to it, and let the current workflow continue. That handle is the fiber.

```fsharp
let loadBoth left right =
    flow {
        let! leftFiber = Flow.fork left
        let! rightValue = right
        let! leftValue = Flow.join leftFiber
        return leftValue, rightValue
    }
```

The example starts `left` in the background, runs `right` in the current workflow, then joins the child fiber before returning.

## Structured Concurrency

Fibers are the foundation of **Structured Concurrency** in Axial. Unlike "fire-and-forget" background tasks, Fibers allow you to maintain a parent-child relationship between workflows, ensuring that background work is always accounted for and safely cleaned up.

The primary operations for managing fibers are:

- [**`Flow.fork`**]({{< relref "/flow/reference/flow/concurrency/m-flow-fork.md" >}}): starts a flow in the background and returns a `Fiber<'error, 'value>` handle.
- [**`Flow.join`**]({{< relref "/flow/reference/flow/concurrency/m-flow-join.md" >}}): waits for the fiber and resumes with its successful value or typed failure.
- [**`Flow.interrupt`**]({{< relref "/flow/reference/flow/concurrency/m-flow-interrupt.md" >}}): asks the fiber to stop, then waits for the child workflow to report its final `Exit`.
- [**`Flow.forkDetached`**]({{< relref "/flow/reference/flow/concurrency/m-flow-flow-forkdetached.md" >}}): starts deliberate fire-and-forget work whose defects are never reported as unobserved.
- `Fiber.dump`: returns a diagnostic snapshot of the fiber id, parent id, start time, and current status.

Joining or interrupting a fiber marks its outcome as observed. A fiber whose handle is simply discarded and that later dies with a defect is reported through the runtime's [fiber observer]({{< relref "supervision.md" >}}); use `Flow.forkDetached` when the silence is intentional, and [`Flow.Runtime.supervise`]({{< relref "supervision.md" >}}) to restart background work that dies with defects.

## Why Fibers?

Fibers provide several advantages over raw `Task` or `Async` values:

### Interruption

In ordinary .NET code, cancellation often depends on manually threading a `CancellationToken` through every layer. In Axial, interruption is part of the execution model. `Flow.interrupt` signals the child fiber and waits for it to finish, so callers can observe the final `Exit<'value, 'error>`.

### Typed Outcomes

A `Fiber<'error, 'value>` remembers the error type and success type of the workflow it is running. When you `Flow.join` a fiber, the joined flow has the same typed failure channel as the child.

### Clear Ownership

Fibers make background work visible in the workflow that started it. If the parent needs the result, it joins. If the parent no longer needs the result, it interrupts. That is different from launching an untracked task and hoping some other layer notices when it fails.

### Diagnostics

Every forked fiber carries metadata:

- `FiberId`: A unique runtime id for the child fiber.
- `ParentId`: The id of the fiber that called `Flow.fork`.
- `StartedAt`: The UTC time when the fiber started.
- `Status`: `Running`, `Succeeded`, `Failed`, or `Interrupted`.

Use `Fiber.dump` when logging or debugging fiber behavior. The dump is a snapshot, so a running fiber can report `Running` before `Flow.join` and `Succeeded`, `Failed`, or `Interrupted` afterward.

## Underlying Implementation

On .NET, a fiber is a small record around a `Task<Exit<'value, 'error>>`, a `CancellationTokenSource`, and diagnostic metadata. On Fable, it wraps an `Async<Exit<'value, 'error>>` with the same public model.

```fsharp
type Fiber<'error, 'value> =
    {
        ExitTask: Task<Exit<'value, 'error>> // The running work
        InterruptSource: CancellationTokenSource // The kill switch
        Metadata: FiberMetadata // Diagnostic identity and lifecycle state
    }
```

This keeps the public model the same while still using the platform's native execution primitive underneath.

## Concurrency Primitives

Most code should not manage fibers manually. Prefer high-level parallel combinators when they express the whole relationship:

- `Flow.zipPar`: Runs two flows concurrently in separate fibers and waits for both.
- `Flow.race`: Runs two flows concurrently and returns the result of the winner, interrupting the loser.

Use explicit fibers when the parent workflow needs to start child work, do something else, and decide later whether to join or interrupt it.
