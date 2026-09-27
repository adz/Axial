---
title: Fibers
description: Lightweight logical threads and structured concurrency in Axial.
---

# Fibers

Fibers represent running child workflows.

In Axial, a **Fiber** is a handle to a running [**`Flow`**](/api/). A flow is a cold description of work. A fiber is the hot execution that exists after that work has been started in the background.

## The Mental Model

While a `Flow` is **cold** (a description of work that hasn't started yet), a **Fiber** is **hot** (the work is currently being executed).

When you fork a flow, you are saying: start this work now, give me a typed handle to it, and let the current workflow continue. That handle is the fiber.

```fsharp
let loadBoth left right =
    flow {
        let! leftFiber = Flow.fork left
        let! rightValue = right
        let! leftValue = Fiber.join leftFiber
        return leftValue, rightValue
    }
```

The example starts `left` in the background, runs `right` in the current workflow, then joins the child fiber before returning.

## Structured Concurrency

Fibers are the foundation of **Structured Concurrency** in Axial. Unlike "fire-and-forget" background tasks, Fibers allow you to maintain a parent-child relationship between workflows, ensuring that background work is always accounted for and safely cleaned up.

The primary operations for managing fibers are:

- [**`Flow.fork`**](/api/): starts a flow in the background and returns a `Fiber<'error, 'value>` handle.
- [**`Fiber.join`**](/api/): waits for the fiber and resumes with its successful value or typed failure.
- [**`Fiber.await`**](/api/): waits for the fiber and returns its `Exit` without failing, for callers that decide what the outcome means.
- `Fiber.poll`: returns the fiber's `Exit` if it has settled, without waiting.
- [**`Fiber.interrupt`**](/api/): asks the fiber to stop, then waits for the child workflow to report its final `Exit`.
- [**`Flow.forkDetached`**](/api/): starts deliberate fire-and-forget work whose defects are never reported as unobserved.
- `Flow.forkNamed`: forks with a diagnostic name that carries into dumps and telemetry fiber spans, so long-lived background fibers are recognizable instead of bare ids.
- `Fiber.dump`: returns a diagnostic snapshot of one fiber handle.

- `Flow.forkGraceful stop grace`: forks a fiber that, when its scope closes, is asked to stop with `stop` and given
  up to `grace` to finish before it is interrupted. See [stopping a consumer gracefully](#stopping-a-consumer-gracefully).

Joining, awaiting, or interrupting a fiber marks its outcome as observed. A fiber whose handle is simply discarded and that later dies with a defect is reported through the runtime's [fiber observer](./supervision.html); use `Flow.forkDetached` when the silence is intentional, and [`Flow.supervise`](./supervision.html) to restart background work that dies with defects.

## Why Fibers?

Fibers provide several advantages over raw `Task` or `Async` values:

### Interruption

In ordinary .NET code, cancellation often depends on manually threading a `CancellationToken` through every layer. In Axial, interruption is part of the execution model. `Fiber.interrupt` signals the child fiber and waits for it to finish, so callers can observe the final `Exit<'value, 'error>`.

### Typed Outcomes

A `Fiber<'error, 'value>` remembers the error type and success type of the workflow it is running. When you `Fiber.join` a fiber, the joined flow has the same typed failure channel as the child.

### Clear Ownership

Fibers make background work visible in the workflow that started it. If the parent needs the result, it joins. If the parent no longer needs the result, it interrupts. An untracked task, by contrast, fails unnoticed unless some other layer checks it.

### Diagnostics

Every forked fiber carries metadata:

- `FiberId`: A unique runtime id for the child fiber.
- `Name`: The diagnostic name from `Flow.forkNamed`, if one was given.
- `ParentId`: The id of the fiber that called `Flow.fork`.
- `Annotations`: The runtime annotations (`Flow.annotate`) in scope at the fork site.
- `StartedAt` / `SettledAt`: UTC timestamps for fork and settle.
- `Status`: `Running`, `Succeeded`, `Failed`, or `Interrupted`.

Use `Fiber.dump` when logging or debugging one fiber. The dump is a snapshot, so a running fiber can report `Running` before `Fiber.join` and `Succeeded`, `Failed`, or `Interrupted` afterward. To see every live fiber at once as a parent/child tree, install a `FiberRegistry` with `Flow.withFiberRegistry` and call `registry.Dump()`; see [Observability](/observability/index.html).

## Underlying Implementation

On .NET, a fiber wraps a `Task<Exit<'value, 'error>>`, a `CancellationTokenSource`, and diagnostic metadata. On Fable, it wraps an `Async<Exit<'value, 'error>>` with the same public model. Only `Metadata` is public; the task and cancellation source are reached through the `Fiber` functions, so observation and interruption always go through the runtime's bookkeeping.

## Concurrency Primitives

Most code should not manage fibers manually. Prefer high-level parallel combinators when they express the whole relationship:

- `Flow.zipPar`: Runs two flows concurrently in separate fibers and waits for both.
- `Flow.race`: Runs two flows concurrently and returns the result of the winner, interrupting the loser.
- `Flow.traversePar`: Maps many values with bounded concurrency and returns the results in input order.
- `Flow.forEachPar`: Runs a flow for each value with bounded concurrency, discarding the results.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let! pages = urls |> Flow.traversePar (Parallelism.bounded 8) fetchPage
do! files |> Flow.forEachPar (Parallelism.ofProcessors id) indexFile
```

At most the given number of flows run at once, and each worker starts the next value as soon as it finishes one. The first failure interrupts the flows still running and waits for their cleanup, so no sibling keeps running after the traversal has failed. Size CPU-bound work with `Parallelism.ofProcessors`, which clamps to at least 1.

When each worker needs its own connection or handle, use `Flow.traverseParUsing` or `Flow.forEachParUsing` with a
`Resource`. Each worker acquires the resource once when it starts, reuses it for every value it takes, and releases it
when it finishes or the traversal fails, so at most `parallelism` resources exist at once:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let openReader = Resource.create (Flow.fromBlocking (fun _ -> repository.OpenReader())) (fun reader _ -> reader.DisposeAsync().AsTask())

let! matches = commits |> Flow.traverseParUsing (Parallelism.ofProcessors id) openReader searchCommit
```

`FlowStream.mapFlowParUsing` does the same for a stream, lending each running mapping a resource from a pool of at
most `parallelism`.

Use explicit fibers when the parent workflow needs to start child work, do something else, and decide later whether to join or interrupt it.

## Latest Wins

When only the newest request matters, such as a search box or autocomplete, hold the running fiber in a `FiberSlot` and
fork with `Flow.forkReplacing`. Each fork signals the previous fiber in the slot to stop and does not wait for it, so
the new request starts at once:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let! slot = FiberSlot.make ()

let onQueryChanged query =
    search query |> Flow.forkReplacing slot |> Flow.ignore
```

`Flow.forkReplacingKey key slots` does the same per key, with a slot from `FiberSlot.makeKeyed`, so a new preview for
one document replaces only that document's previous load. A key's entry is removed when its fiber settles.
`FiberSlot.interrupt` and `FiberSlot.interruptAll` stop what is running and wait for its cleanup, for example on
shutdown. `FiberSlot.count` returns how many keyed fibers are running. For a stream of inputs,
`FlowStream.switchMapFlow` applies the same rule inside the stream.

## What a fiber owns

Each forked fiber runs in its own scope, a child of the scope that forked it. Whatever the fiber acquires, such as a
resource registered with `Flow.scopeAcquireRelease`, a [hub subscription](./hub.html), or a
[scoped queue](./queue.html#tying-a-queue-to-a-scope), is released when the fiber settles, not when the parent scope
eventually closes. A consumer fiber that ends therefore cannot leave a subscription behind that holds up a publisher.

Closing the parent scope interrupts fibers that are still running, then releases what they acquired.

## Stopping a consumer gracefully

Interrupting a consumer the moment its scope closes throws away its backlog. `Flow.forkGraceful` changes what closing
the scope does: it runs a stop request, waits up to a grace period for the fiber to finish, and interrupts it only if it
is still running after that. For a queue consumer, the stop request is `Dequeue.shutdown`: the consumer's stream then
ends normally once it has drained the queue.

```fsharp transcript
> (flow {
-     let written = ResizeArray<int>()
-     do!
-         flow {
-             let! (samples: Queue<int>) = Queue.bounded 100
-             let! _ =
-                 samples
-                 |> FlowStream.fromDequeue
-                 |> FlowStream.runForEach written.Add
-                 |> Flow.forkGraceful (Dequeue.shutdown samples) (System.TimeSpan.FromSeconds 5.0)
-             do! samples |> Queue.offerAll [ 1..5 ] |> Flow.ignore
-         }
-         |> Flow.scoped
-     return List.ofSeq written
- } : Flow<unit, Never, int list>)
- |> Flow.run ();;
val it: Exit<int list,Never> = Success [1; 2; 3; 4; 5]
```

The scope closed right after the offers, before the consumer had necessarily taken any of them. Closing it shut the
queue down, and the consumer finished all five values before the scope finished closing. With a plain `Flow.fork`, the
consumer would have been interrupted with part of the backlog still queued.

The [pipeline torture test](torture-tests/pipeline.html) stops a control loop and a historian this way and checks that no sample is lost.

A graceful fiber is also not interrupted when the flow that forked it is interrupted: its stop request runs when that
flow's scope closes, so a consumer still flushes when the application is cancelled. `Fiber.interrupt` still interrupts
it immediately.

