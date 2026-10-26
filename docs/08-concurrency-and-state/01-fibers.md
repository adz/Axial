---
title: Fibers
description: Lightweight logical threads and structured concurrency in Axial.
---

# Fibers

A **Fiber** is a handle to a running [**`Flow`**](/api/). A flow describes work; a fiber represents its execution after it starts in the background.

## Fiber-local values

`FiberLocal.make initial` creates an unnamed local. `FiberLocal.named "jobId" initial` creates one whose explicitly scoped value can be attached to log entries. `FiberLocal.get local` reads the current value or its initial value. `FiberLocal.locally value local work` overrides it for `work` and restores the prior value afterward. Child fibers inherit the value present when forked; concurrent siblings do not share changes. `FiberLocal.current ()` synchronously returns only named locals with an explicitly scoped value, as `(name, value)` pairs. Unset defaults and unnamed locals are excluded.

## The Mental Model

A `Flow` is **cold** until it starts. Forking starts it and returns a **Fiber** handle while the current workflow continues.

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
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
let loadBoth left right =
    flow {
        let! leftFiber = Flow.fork left
        let! rightValue = right
        let! leftValue = Fiber.join leftFiber
        return leftValue, rightValue
    }
```

```fsharp run
loadBoth (Flow.ok 1 : Flow<ClockEnvironment, string, int>) (Flow.ok "two") |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success(1, "two"))
```

The example starts `left` in the background, runs `right` in the current workflow, then joins the child fiber before returning.

## Structured Concurrency

Forked fibers retain a parent-child relationship: closing the parent scope interrupts running children and releases their scoped resources.

Fiber operations:

- [**`Flow.fork`**](/api/): starts a flow in the background and returns a `Fiber<'error, 'value>` handle.
- `Flow.forkResource`: starts acquiring a `Resource` in the background; joining the fiber returns the resource, still
  open and owned by the caller's scope.
- [**`Fiber.join`**](/api/): waits for the fiber and resumes with its successful value or typed failure.
- [**`Fiber.awaitExit`**](/api/): waits for the fiber and returns its `Exit` without failing, for callers that decide what the outcome means.
- `Fiber.poll`: returns the fiber's `Exit` if it has settled, without waiting.
- `Fiber.requestInterrupt`: asks the fiber to stop and returns immediately. Use it before a bounded race when foreign work may ignore interruption. Cancellation-callback exceptions are contained and reported through the fiber observer.
- [**`Fiber.interrupt`**](/api/): asks the fiber to stop, then waits for the child workflow to report its final `Exit`.
- [**`Flow.forkDetached`**](/api/): starts deliberate fire-and-forget work whose defects are never reported as unobserved.
- `Flow.forkNamed`: forks with a diagnostic name that carries into dumps and telemetry fiber spans, so long-lived background fibers are recognizable instead of bare ids.
- `Fiber.dump`: returns a diagnostic snapshot of one fiber handle.

- `Flow.forkGraceful stop grace`: forks a fiber that, when its scope closes, is asked to stop with `stop` and given
  up to `grace` to finish before it is interrupted. See [stopping a consumer gracefully](#stopping-a-consumer-gracefully).

### A fiber owns its scoped resources

A forked fiber runs in its own [scope](/scopes/index.html). The [scoped resources](/scopes/index.html#scoped-resources)
it acquires, such as a value from `Flow.scopeResource` or a hub subscription, are released when the fiber settles,
whether or not anything joins it. `Fiber.join` gives you the fiber's value, not its resources, so a resource the fiber
returns has already been released:

```fsharp
/// Wrong: the fiber releases the connection when it settles, before join returns it.
let joinForkedConnection (connection: Resource<ClockEnvironment, string, string>) =
    flow {
        let! fiber = Flow.scopeResource connection |> Flow.fork
        return! Fiber.join fiber // already released
    }

/// Right: only the acquisition runs in the fiber, and the caller's scope owns the connection.
let joinKeptConnection (connection: Resource<ClockEnvironment, string, string>) =
    flow {
        let! connecting = Flow.forkResource connection
        return! Fiber.join connecting // still open
    }
```

Whatever the fiber acquires — a value from `Flow.scopeResource`, a resource registered with
`Flow.scopeAcquireRelease`, a [hub subscription](./hub.html), or a
[scoped queue](./queue.html#tying-a-queue-to-a-scope) — is released when the fiber settles, not when the parent scope
eventually closes. A consumer fiber that ends therefore cannot leave a subscription behind that holds up a publisher.
Closing the parent scope interrupts fibers that are still running, then releases what they acquired.

`Flow.forkResource` differs: only the acquisition runs in the fiber, and the resource's release is registered with
the caller's scope. If that scope closes before the acquisition finishes, the fiber is interrupted and a resource
acquired anyway is released at once. To acquire several resources in parallel, use `Flow.zipPar` or
`Flow.sequencePar`; see [Concurrency and scoped resources](/scopes/index.html#concurrency-and-scoped-resources) for
what each concurrent combinator does with scoped resources.

Joining, awaiting, or interrupting a fiber marks its outcome as observed. A fiber whose handle is simply discarded and that later dies with a defect is reported through the runtime's [fiber observer](./supervision.html); use `Flow.forkDetached` when the silence is intentional, and [`Flow.supervise`](./supervision.html) to restart background work that dies with defects.

## Why Fibers?

Compared with raw `Task` or `Async` values, fibers provide:

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

Use `Fiber.dump` when logging or debugging one fiber. The dump is a snapshot, so a running fiber can report `Running` before `Fiber.join` and `Succeeded`, `Failed`, or `Interrupted` afterward. To see every live fiber at once as a parent/child tree, install a `FiberRegistry` with `Flow.withFiberRegistry` and call `registry.DumpAt(clock)`; see [Observability](/observability/index.html).

## Underlying Implementation

On .NET, a fiber wraps a `Task<Exit<'value, 'error>>`, a `CancellationTokenSource`, and diagnostic metadata. On Fable, it wraps an `Async<Exit<'value, 'error>>` with the same public model. Only `Metadata` is public; the task and cancellation source are reached through the `Fiber` functions, so observation and interruption always go through the runtime's bookkeeping.

## Concurrency Primitives

Use parallel combinators when they express the whole relationship:

- `Flow.zipPar`: Runs two flows concurrently and waits for both.
- `Flow.race`: Runs two flows concurrently, interrupts and awaits the loser, closes its scope, then returns the winner's result. A loser defect becomes part of the returned cause.
- `Flow.disconnect`: Makes interruption stop waiting promptly while the interrupted work finishes in a detached owned fiber. Its lifecycle is still observed, and a late defect is reported once as unobserved. Use it only after a bounded structured wait at a forced-close boundary where foreign work may ignore interruption.
- `Flow.traversePar`: Maps many values with bounded concurrency and returns the results in input order.
- `Flow.forEachPar`: Runs a flow for each value with bounded concurrency, discarding the results.

Unlike `Flow.fork`, these return values, and scoped resources come with the values: what a flow acquires moves to the
caller's scope when you receive its value, and is released when you don't (a race loser, or every branch of a
`zipPar` or traversal that failed). `Flow.race` does not return until loser cleanup finishes, so replacement work may
immediately reuse an exclusive resource. `Flow.zipPar (Flow.scopeResource a) (Flow.scopeResource b)` opens two resources
in parallel and both stay open until the caller's scope closes.

```fsharp
let fetchPage (url: string) : Flow<ClockEnvironment, string, int> =
    Flow.sleep (TimeSpan.FromMilliseconds 1.0) |> Flow.map (fun () -> url.Length)

let pageSizes (urls: string list) : Flow<ClockEnvironment, string, int list> =
    urls |> Flow.traversePar (Parallelism.bounded 8) fetchPage

let indexFile (indexed: Ref<string list>) (file: string) : Flow<ClockEnvironment, string, unit> =
    indexed |> Ref.update (fun files -> file :: files)

/// Indexes every file in parallel and returns the indexed files, sorted.
let indexAll (files: string list) : Flow<ClockEnvironment, string, string list> =
    flow {
        let! indexed = Ref.make []
        do! files |> Flow.forEachPar (Parallelism.ofProcessors id) (indexFile indexed)
        let! indexedFiles = Ref.get indexed
        return List.sort indexedFiles
    }
```

```fsharp run
pageSizes [ "a"; "bb"; "ccc" ] |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success [ 1; 2; 3 ])

indexAll [ "b.fs"; "a.fs"; "c.fs" ] |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success [ "a.fs"; "b.fs"; "c.fs" ])
```

`traversePar` returns results in input order, whatever order the work finishes in.

At most the given number of flows run at once, and each worker starts the next value as soon as it finishes one. The
workers start each mapping inline, which avoids a scheduler hop for ordinary I/O flows. A synchronous CPU-bound
mapping must opt into the thread pool so that the first worker does not finish every item before the other workers
start:

```fsharp
let calculate (value: int) : Flow<ClockEnvironment, string, int> =
    Flow.delay (fun () -> Flow.ok (value * value))

let calculatePar values =
    values
    |> Flow.traversePar (Parallelism.ofProcessors id) (fun value ->
        calculate value |> Flow.runOn RunOn.ThreadPool)
```

```fsharp run
calculatePar [ 1; 2; 3 ]
|> Flow.run (ClockEnvironment Clock.live)
|> shouldEqual (Exit.Success [ 1; 4; 9 ])
```

Use `Flow.runOn RunOn.ThreadPool` around the mapping flow, not around the whole traversal; this lets the bounded workers overlap
while preserving the requested limit. `Parallelism.ofProcessors` clamps the bound to at least 1. The first failure
interrupts the flows still running and waits for their cleanup, so no sibling keeps running after the traversal has
failed.

When each worker needs its own connection or handle, use `Flow.traverseParUsing` or `Flow.forEachParUsing` with a
`Resource`. Each worker acquires the resource once when it starts, reuses it for every value it takes, and releases it
when it finishes or the traversal fails, so at most `parallelism` resources exist at once:

```fsharp
/// Counts readers opened and closed.
type ReaderCounts = { Opened: int; Closed: int }

let openReader (counts: Ref<ReaderCounts>) : Resource<ClockEnvironment, string, int> =
    Resource.create
        (counts |> Ref.updateAndGet (fun c -> { c with Opened = c.Opened + 1 }) |> Flow.map _.Opened)
        (fun _ -> counts |> Ref.update (fun c -> { c with Closed = c.Closed + 1 }))

let searchCommit (reader: int) (commit: int) : Flow<ClockEnvironment, string, bool> = Flow.ok (commit % 2 = 0)

/// Searches the commits with at most two readers, and returns the results with the reader counts.
let evenCommits (commits: int list) : Flow<ClockEnvironment, string, bool list * ReaderCounts> =
    flow {
        let! counts = Ref.make { Opened = 0; Closed = 0 }
        let! results = commits |> Flow.traverseParUsing (Parallelism.bounded 2) (openReader counts) searchCommit
        let! final = Ref.get counts
        return results, final
    }
```

```fsharp run
match evenCommits [ 1..6 ] |> Flow.run (ClockEnvironment Clock.live) with
| Exit.Success(results, counts) ->
    results |> shouldEqual [ false; true; false; true; false; true ]
    counts.Opened <= 2 |> shouldEqual true
    counts.Closed |> shouldEqual counts.Opened
| other -> failwithf "Expected success, got %A" other
```

`FlowStream.mapFlowParUsing` does the same for a stream, lending each running mapping a resource from a pool of at
most `parallelism`.

Use explicit fibers when the parent workflow needs to start child work, do something else, and decide later whether to join or interrupt it.

## Latest Wins

When only the newest request matters, such as a search box or autocomplete, hold the running fiber in a `FiberSlot` and
fork with `Flow.forkReplacing`. Each fork interrupts the previous fiber in the slot, and the new request starts as
soon as that fiber has finished its cleanup, so the two never overlap. A request that is itself replaced before it
starts never runs:

```fsharp
let search (query: string) : Flow<ClockEnvironment, string, string> =
    Flow.sleep (TimeSpan.FromMilliseconds 50.0) |> Flow.map (fun () -> $"results for {query}")

/// Forks a search per keystroke into one slot, and reports the last result and whether each earlier search stopped.
let typeAhead (queries: string list) : Flow<ClockEnvironment, string, string * bool list> =
    flow {
        let! slot = FiberSlot.make ()
        let! searches = queries |> Flow.traverse (fun query -> search query |> Flow.forkReplacing slot)
        let! last = searches |> List.last |> Fiber.join
        let! earlier = searches |> List.take (searches.Length - 1) |> Flow.traverse Fiber.awaitExit
        return last, earlier |> List.map (fun exit -> match exit with Exit.Failure cause -> Cause.isInterrupted cause | _ -> false)
    }
```

```fsharp run
typeAhead [ "a"; "ax"; "axi" ] |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success("results for axi", [ true; true ]))
```

`Flow.forkReplacingKey key slots` does the same per key, with a slot from `FiberSlot.makeKeyed`, so a new preview for
one document replaces only that document's previous load. A key's entry is removed when its fiber settles.
`FiberSlot.interrupt` and `FiberSlot.interruptAll` stop what is running and wait for its cleanup, for example on
shutdown. A defect in a replaced or interrupted fiber is reported once to the fiber observer, because no caller reads
its outcome. `FiberSlot.count` returns how many keyed fibers are running. For a stream of inputs,
`FlowStream.switchMapFlow` applies the same rule inside the stream.

## Stopping a consumer gracefully

Interrupting a consumer the moment its scope closes throws away its backlog. `Flow.forkGraceful` changes what closing
the scope does: it runs a stop request, waits up to a grace period for the fiber to finish, and interrupts it only if it
is still running after that. For a queue consumer, the stop request is `Dequeue.shutdown`: the consumer's stream then
ends normally once it has drained the queue.

```fsharp transcript
> open Axial.PlatformService;;
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
- } : Flow<ClockEnvironment, Never, int list>)
- |> Flow.run (ClockEnvironment Clock.live);;
val it: Exit<int list,Never> = Success [1; 2; 3; 4; 5]
```

The scope closed right after the offers, before the consumer had necessarily taken any of them. Closing it shut the
queue down, and the consumer finished all five values before the scope finished closing. With a plain `Flow.fork`, the
consumer would have been interrupted with part of the backlog still queued.

The [pipeline torture test](torture-tests/pipeline.html) stops a control loop and a historian this way and checks that no sample is lost.

A graceful fiber is also not interrupted when the flow that forked it is interrupted: its stop request runs when that
flow's scope closes, so a consumer still flushes when the application is cancelled. `Fiber.interrupt` still interrupts
it immediately.
