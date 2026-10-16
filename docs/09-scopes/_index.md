---
title: Scopes
description: Choose lexical or runtime-owned resource lifetimes.
---

# Scopes

.NET already has resources, and F# already has `use` and `use!`. Axial's `flow { }` computation expression supports
both directly:

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
let readFirstLine path =
    flow {
        use reader = File.OpenText path
        return! ColdTask(fun _ -> reader.ReadLineAsync())
    }
```

This is a **lexical lifetime**. The compiler disposes `reader` when control leaves the body governed by that `use`. In a
`flow { }`, disposal happens before the Flow produced by that body completes. The resource cannot safely escape for
another function or later workflow to use.

A Flow scope is a runtime ownership boundary. It lets resources and child fibers live across function and subflow calls,
then closes all of them together after success, failure, interruption, or defect.

## Scoped resources

A **scoped resource** is anything whose cleanup is registered with a Flow scope instead of a lexical `use`:

- a value acquired with `Flow.scopeResource` or `Flow.scopeAcquireRelease`
- cleanup registered with `Flow.scopeFinalizer`, `Flow.scopeAsyncFinalizer`, `Flow.scopeDisposable`, or
  `Flow.scopeAsyncDisposable`
- what Axial registers for you: a [hub subscription](/concurrency-and-state/hub.html), a queue from
  `Queue.makeScoped`, and a fiber from `Flow.fork`, which the scope interrupts if it is still running

A scoped resource is released when the scope it was registered with closes. This page is about which scope that is:
the current one, a child created with `Flow.scoped`, or, in concurrent code, the scope of a branch or fiber (see
[Concurrency and scoped resources](#concurrency-and-scoped-resources)).

## Lifetime maps

The diagrams use containment literally: Flow B is a subflow inside Flow A, and a resource bar sits inside the boundary
that owns it.

### Lexical `use` ends with its subflow body

<div class="lifetime-case" role="img" aria-label="Flow A contains Flow B, whose lexical use resource ends when Flow B ends">
<div class="lifetime-axis"><span>Flow A starts</span><span>time →</span><span>Flow A ends</span></div>
<div class="lifetime-stage lifetime-stage--flow"><strong>Flow A</strong><span class="lifetime-region lifetime-region--subflow" style="--x: 18; --w: 38; --y: 2.4; --h: 4.8"><b>Flow B</b><span class="lifetime-region lifetime-region--lexical" style="--x: 14; --w: 72; --y: 1.8; --h: 2">use reader · dispose</span></span></div>
</div>

Flow B owns `reader` lexically. When Flow B returns to Flow A, `reader` has already been disposed.

### Registration in the current scope outlives Flow B

<div class="lifetime-case" role="img" aria-label="The root scope contains Flow A; Flow B registers a resource that remains owned after Flow B ends and is cleaned when the root scope closes">
<div class="lifetime-axis"><span>execution starts</span><span>time →</span><span>root scope closes</span></div>
<div class="lifetime-stage lifetime-stage--scope"><strong>current root scope</strong><span class="lifetime-region lifetime-region--flow" style="--x: 5; --w: 90; --y: 2.2; --h: 7"><b>Flow A</b><span class="lifetime-region lifetime-region--subflow" style="--x: 12; --w: 34; --y: 1.8; --h: 2.2">Flow B registers R</span><span class="lifetime-region lifetime-region--resource" style="--x: 30; --w: 64; --y: 4.5; --h: 1.7">R remains owned · cleanup</span></span></div>
</div>

Flow B ends, but `R` does not. `Flow.scopeResource` and the other `scope...` functions register with the current scope,
so `R` remains alive while Flow A continues and is cleaned only when that scope closes.

### `Flow.scoped` creates an earlier cleanup boundary

<div class="lifetime-case" role="img" aria-label="Flow A contains a child scope; Flow B registers a resource in it, Flow B ends, then the child scope cleans the resource before Flow A ends">
<div class="lifetime-axis"><span>Flow A starts</span><span>time →</span><span>Flow A ends</span></div>
<div class="lifetime-stage lifetime-stage--flow"><strong>Flow A</strong><span class="lifetime-region lifetime-region--scope" style="--x: 12; --w: 70; --y: 2.2; --h: 7"><b>Flow.scoped child</b><span class="lifetime-region lifetime-region--subflow" style="--x: 10; --w: 38; --y: 1.8; --h: 2.2">Flow B registers R</span><span class="lifetime-region lifetime-region--resource" style="--x: 27; --w: 66; --y: 4.5; --h: 1.7">R remains owned · cleanup</span></span></div>
</div>

Here Flow B still ends before `R` does, but the child scope closes before Flow A ends. This is the reason for
`Flow.scoped`: it chooses a runtime cleanup boundary independently of the function or subflow that acquired the
resource.

## Create a local runtime scope

```fsharp
/// A stand-in connection that records when it is opened and closed.
let lifecycle = ResizeArray<string>()

type Connection(name: string) =
    member _.Name = name

let openConnection : Flow<string, Connection> =
    Flow.delay (fun () ->
        lifecycle.Add "open"
        Flow.ok (Connection "orders"))

let closeConnection (connection: Connection) (_: CancellationToken) : Task =
    lifecycle.Add $"close {connection.Name}"
    Task.CompletedTask

let runApplicationWork (connection: Connection) : Flow<string, string> =
    Flow.delay (fun () ->
        lifecycle.Add "work"
        Flow.ok $"used {connection.Name}")

let application : Flow<string, string> =
    Flow.scoped (
        flow {
            let! connection =
                Flow.scopeAcquireRelease
                    openConnection
                    closeConnection

            return! runApplicationWork connection
        })
```

```fsharp run
lifecycle.Clear()
application |> Flow.run () |> shouldEqual (Exit.Success "used orders")
List.ofSeq lifecycle |> shouldEqual [ "open"; "work"; "close orders" ]
```

Everything registered inside `Flow.scoped` remains available across calls made inside that block. Cleanup finishes
before the resulting Flow returns.

## Register with the current scope

The `scope` prefix means “attach this value or cleanup action to the current Flow scope”:

```fsharp
let registerEverything (stream: IDisposable) (response: IAsyncDisposable) : Flow<string, unit> =
    flow {
        do! Flow.scopeDisposable stream
        do! Flow.scopeAsyncDisposable response
        do! Flow.scopeFinalizer (fun _ -> lifecycle.Add "flush telemetry"; Task.CompletedTask)
        do! Flow.scopeAsyncFinalizer (fun _ -> async { lifecycle.Add "save state" })
    }
```

Finalizers run in the reverse of their registration order when the scope closes:

```fsharp run
lifecycle.Clear()

let stream = { new IDisposable with member _.Dispose() = lifecycle.Add "dispose stream" }
let response = { new IAsyncDisposable with member _.DisposeAsync() = lifecycle.Add "dispose response"; ValueTask() }

registerEverything stream response |> Flow.scoped |> Flow.run () |> shouldEqual (Exit.Success())
List.ofSeq lifecycle |> shouldEqual [ "save state"; "flush telemetry"; "dispose response"; "dispose stream" ]
```

Use `Flow.scopeDisposable` and `Flow.scopeAsyncDisposable` for resources that already exist. Use
`Flow.scopeFinalizer` or `Flow.scopeAsyncFinalizer` for custom cleanup.

`Flow.scopeAcquireRelease` combines acquisition and registration without an interruption point between them:

```fsharp
type RequestCache() =
    member _.Entries = Collections.Generic.Dictionary<string, string>()

    interface IDisposable with
        member _.Dispose() = lifecycle.Add "cache disposed"

let acquireRequestCache : Flow<string, RequestCache> =
    Flow.scopeAcquireRelease
        (Flow.succeed (new RequestCache()))
        (fun cache _ ->
            (cache :> IDisposable).Dispose()
            Task.CompletedTask)
```

The returned value remains available to later subflows in the same scope.

## Reusable resource descriptions

`Resource` separates a reusable acquisition description from the scope that eventually owns it:

```fsharp
let connectionResource = Resource.create openConnection closeConnection

let query (connection: Connection) : Flow<string, int> = Flow.ok connection.Name.Length

let program : Flow<string, int> =
    Flow.scoped (
        flow {
            let! connection =
                connectionResource
                |> Flow.scopeResource

            return! query connection
        })
```

```fsharp run
lifecycle.Clear()
program |> Flow.run () |> shouldEqual (Exit.Success 6)
List.ofSeq lifecycle |> shouldEqual [ "open"; "close orders" ]
```

Use `Resource.finalizer` for task-based cleanup and `Resource.asyncFinalizer` for F# async cleanup.

## Concurrency and scoped resources

Every concurrent flow runs in a scope of its own: each branch of `Flow.zipPar` or `Flow.race`, each item of
`Flow.traversePar`, and each fiber from `Flow.fork`. What decides whether its scoped resources outlive it is one rule:

> **If you receive a concurrent flow's value, you also receive its scoped resources. If you don't, they are released.**

"Receiving the scoped resources" means they move to the caller's scope and behave exactly as if the caller had
acquired them: they stay open for the caller's later steps and are released when the caller's scope closes.

| Combinator | What happens to its scoped resources |
| --- | --- |
| `Flow.zipPar`, `Flow.sequencePar`, `Flow.traversePar`, `Flow.forEachPar` | Move to the caller's scope when every flow succeeds. Released before it returns when any flow fails. |
| `Flow.race` | The winner's move to the caller's scope if it succeeds. The loser's are released once it has been interrupted. |
| `Flow.timeout`, `Flow.timeoutToOk`, `Flow.timeoutWith` | Move to the caller's scope when the flow succeeds in time. Released when it fails or times out. |
| `Flow.fork` and its variants | Stay with the fiber and are released when it settles. `Fiber.join` gives you the value, not the resources. |
| `Flow.forkResource` | The resource is acquired in a fiber and its release goes to the caller's scope. |
| The per-worker resource of `Flow.traverseParUsing` and `Flow.forEachParUsing` | Released when that worker finishes. |

`Flow.fork` is the combinator that returns a handle instead of a value. Nothing tells the fiber whether anyone will
join it, so it releases its scoped resources when it settles. That is what stops a finished consumer from holding a hub
subscription open and stalling the publisher.

### Acquire in parallel

Acquire several scoped resources at once with `Flow.zipPar` or `Flow.sequencePar`. Both stay open after the
combinator returns:

```fsharp
/// Records each open and close; the lock is there because the two opens run in parallel.
let connectionLog = ResizeArray<string>()
let log entry = lock connectionLog (fun () -> connectionLog.Add entry)

let connectionTo (name: string) : Resource<ClockEnvironment, string, string> =
    Resource.create
        (Flow.delay (fun () -> log $"open {name}"; Flow.ok name))
        (fun _ _ -> log $"close {name}"; Task.CompletedTask)

let closedCount () = lock connectionLog (fun () -> connectionLog |> Seq.filter (fun entry -> entry.StartsWith "close") |> Seq.length)

/// Opens two connections in parallel and reports how many were closed while they were in use.
let openBoth : Flow<ClockEnvironment, string, int> =
    flow {
        let! orders, customers =
            Flow.zipPar (Flow.scopeResource (connectionTo "orders")) (Flow.scopeResource (connectionTo "customers"))

        return closedCount ()
    }
    |> Flow.scoped
```

```fsharp run
connectionLog.Clear()
openBoth |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success 0)
closedCount () |> shouldEqual 2
```

No connection closed while `openBoth` used them; both closed when its scope did.

### Acquire in the background and keep it

To start acquiring one resource, do other work, and use the resource later, use `Flow.forkResource`. The acquisition
runs in a fiber, but the release belongs to the caller's scope, so `Fiber.join` returns a live resource. A plain
`Flow.fork` of the same acquisition returns a resource that the fiber has already released:

```fsharp
/// A plain fork: the fiber owns the connection and releases it when it settles, before join returns it.
let forkedConnectionClosedOnJoin : Flow<ClockEnvironment, string, bool> =
    flow {
        let! fiber = Flow.scopeResource (connectionTo "audit") |> Flow.fork
        let! _ = Fiber.join fiber
        return lock connectionLog (fun () -> connectionLog.Contains "close audit")
    }
    |> Flow.scoped

/// forkResource: the caller's scope owns the connection, so it is still open after join.
let keptConnectionClosedOnJoin : Flow<ClockEnvironment, string, bool> =
    flow {
        let! connecting = Flow.forkResource (connectionTo "audit")
        let! _ = Fiber.join connecting
        return lock connectionLog (fun () -> connectionLog.Contains "close audit")
    }
    |> Flow.scoped
```

```fsharp run
connectionLog.Clear()
forkedConnectionClosedOnJoin |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success true)

connectionLog.Clear()
keptConnectionClosedOnJoin |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success false)
List.ofSeq connectionLog |> shouldEqual [ "open audit"; "close audit" ]
```

If the caller's scope closes before the acquisition finishes, the fiber is interrupted, and a resource acquired anyway
is released straight away. [`Axial.Guardrails`](/notes/guardrails.html#axg007-keep-resources-acquired-by-a-fiber)
reports a forked flow that returns its own scoped resource as `AXG007`.

### Coming from ZIO

In ZIO 2, a forked fiber shares its parent's `Scope`, so `ZIO.acquireRelease(...).fork` followed by `join` returns a
live resource. In Axial a forked fiber owns its scope, like `ZIO.scoped(effect).fork`; write `Flow.forkResource` for
the ZIO pattern. `zipPar`, `race`, and the parallel traversals behave as in ZIO for values you receive, and also
release what a race loser or a failed branch acquired instead of keeping it until the scope closes.

### Nested scopes

A fiber created with `Flow.fork` is itself registered with the current scope. Closing the scope interrupts and awaits an
unfinished fiber, then releases what the fiber acquired. A nested `Flow.scoped` therefore gives a group of resources and
fibers a lifetime shorter than the surrounding application without requiring every function to pass cleanup handles
manually.

Child scopes are also owned by their parent. If the root execution is interrupted, it closes every remaining child in
reverse registration order.

## Streams

[FlowStream](/streams/index.html) terminal consumers create child scopes internally. Stream resources and parallel
mapping fibers close on completion, failure, interruption, or early termination. The
[consuming streams](/streams/consuming.html) guide explains that boundary from the stream user's perspective.

## Rules to remember

- Use `use` or `use!` when one lexical body exclusively owns a disposable.
- Use `Flow.scoped` when ownership spans functions, subflows, fibers, or stream pulls.
- `scope...` functions attach cleanup to the current scope; they do not clean up immediately.
- Ordinary `Flow.bind` and `flow { }` nesting share the current scope.
- In concurrent code, you receive a flow's scoped resources when you receive its value; otherwise they are released.
- A forked fiber releases its scoped resources when it settles. Use `Flow.forkResource` to acquire in the background
  and keep the resource, and `Flow.zipPar` or `Flow.sequencePar` to acquire several in parallel.
- Nested scopes close before their parent.
- Finalizers run in reverse registration order and at most once.
- Cleanup failures are defects and combine with the failure that caused closure.

Layers use the same model. `Layer.acquireRelease` keeps a provisioned service alive until `Layer.provide` finishes.
