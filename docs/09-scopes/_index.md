---
title: Scopes
description: Choose lexical or runtime-owned resource lifetimes.
---

# Scopes

Axial's `flow { }` supports F# `use` and `use!` for .NET resources:

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
`flow { }`, disposal happens before the Flow produced by that body completes. Another function or later workflow cannot
safely use the resource.

`File.OpenText` and `ReadLineAsync` are ambient .NET filesystem calls, not the `IFileSystem` service, so this example is
about lifetime only: a missing file or failed read throws instead of producing a typed error. For the recommended
pattern — an explicit filesystem dependency with a typed error channel — see
[FileSystem](/services/filesystem.html).

A Flow scope is a runtime ownership boundary. It lets resources and child fibers live across function and subflow calls,
then closes all of them together after success, failure, interruption, or defect.

## Scoped resources

A **scoped resource** is anything whose cleanup is registered with a Flow scope instead of a lexical `use`:

- a value acquired with `Flow.scopeResource` or `Flow.scopeAcquireRelease`
- cleanup registered with `Flow.scopeFinalizer`, `Flow.scopeDisposable`, or `Flow.scopeAsyncDisposable`
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

Flow B ends before `R` does; the child scope closes before Flow A ends. `Flow.scoped` sets a cleanup boundary
independently of the function or subflow that acquired the resource.

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

let closeConnection (connection: Connection) : Flow<unit> =
    Flow.delay (fun () ->
        lifecycle.Add $"close {connection.Name}"
        Flow.ok ())

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

Resources registered inside `Flow.scoped` remain available across calls inside that block. Cleanup finishes
before the resulting Flow returns.

### How a release runs

A release is itself a flow, `'value -> Flow<'env, Never, unit>`, so it can update a `Ref`, call a service from the
environment, or wait on asynchronous work. When the scope closes:

- The release runs with the environment the value was acquired with, even if the scope closes somewhere else.
- It runs without interruption. Closing a scope because its flow was interrupted does not cancel the releases.
- Its error type is `Never`, so it cannot fail with a typed error. A release that throws or dies becomes a defect in
  the `Exit` of the flow that owned the scope. All releases still run — they run in reverse registration order, and a
  failing release does not skip the others — and their failures are collected and folded into that `Exit`. If the
  scope closed because its flow had already failed, the release defect is appended to that failure's cause; if the
  flow had succeeded, the release defect alone turns the result into an `Exit.Failure`.

## Register with the current scope

The `scope` prefix means “attach this value or cleanup action to the current Flow scope”:

```fsharp
let registerEverything (stream: IDisposable) (response: IAsyncDisposable) : Flow<string, unit> =
    flow {
        do! Flow.scopeDisposable stream
        do! Flow.scopeAsyncDisposable response
        do! Flow.scopeFinalizer (Flow.delay (fun () -> lifecycle.Add "flush telemetry"; Flow.ok ()))
        do! Flow.scopeFinalizer (Flow.fromAsync (async { lifecycle.Add "save state" }))
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
`Flow.scopeFinalizer` for custom cleanup.

`Flow.scopeAcquireRelease` combines acquisition and registration without an interruption point between them:

```fsharp
type RequestCache() =
    member _.Entries = Collections.Generic.Dictionary<string, string>()

    interface IDisposable with
        member _.Dispose() = lifecycle.Add "cache disposed"

let acquireRequestCache : Flow<string, RequestCache> =
    Flow.scopeAcquireRelease
        (Flow.succeed (new RequestCache()))
        (fun cache ->
            Flow.delay (fun () ->
                (cache :> IDisposable).Dispose()
                Flow.ok ()))
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

Use `Resource.finalizer release` for cleanup that has no acquired value.

## Concurrency and scoped resources

Concurrent flows run in scopes of their own: the flows of one `Flow.zipPar`, `Flow.sequencePar`, or parallel
traversal share a scope, each branch of `Flow.race` has one, and so does each fiber from `Flow.fork`.

- When you receive a concurrent flow's value, its scoped resources move to your scope as if you had acquired them:
  they stay open for your later steps and are released when your scope closes.
- When you don't receive the value, its scoped resources are released.

| Combinator | What happens to its scoped resources |
| --- | --- |
| `Flow.zipPar`, `Flow.sequencePar`, `Flow.traversePar`, `Flow.forEachPar` | Move to the caller's scope when every flow succeeds. Released before it returns when any flow fails. |
| `Flow.race` | The winner's move to the caller's scope if it succeeds. The loser's are released once it has been interrupted. |
| `Flow.timeout`, `Flow.timeoutToOk`, `Flow.timeoutWith` | Move to the caller's scope when the flow succeeds in time. Released when it fails or times out. |
| `Flow.fork` and its variants | Stay with the fiber and are released when it settles. `Fiber.join` gives you the value, not the resources. |
| `Flow.forkResource` | The resource is acquired in a fiber and its release goes to the caller's scope. |
| The per-worker resource of `Flow.traverseParUsing` and `Flow.forEachParUsing` | Released when that worker finishes. |

`Flow.fork` returns a handle instead of a value. The fiber cannot know whether anyone will join it, so it releases
its scoped resources when it settles. This prevents a finished consumer from holding a hub subscription open and
stalling the publisher.

### Acquire in parallel

Acquire several scoped resources at once with `Flow.zipPar` or `Flow.sequencePar`. Both stay open after the
combinator returns:

```fsharp
/// Opens a connection, recording the open and the close in `log`.
let connectionTo (log: Ref<string list>) (name: string) : Resource<ClockEnvironment, string, string> =
    Resource.create
        (log |> Ref.update (fun entries -> entries @ [ $"open {name}" ]) |> Flow.map (fun () -> name))
        (fun _ -> log |> Ref.update (fun entries -> entries @ [ $"close {name}" ]))

let closedIn (log: Ref<string list>) : Flow<ClockEnvironment, string, int> =
    log |> Ref.get |> Flow.map (List.filter (fun (entry: string) -> entry.StartsWith "close") >> List.length)

/// Opens two connections in parallel and counts the closed ones while they are in use and after their scope closes.
let openBoth : Flow<ClockEnvironment, string, int * int> =
    flow {
        let! log = Ref.make []

        let! whileInUse =
            flow {
                let! _orders, _customers =
                    Flow.zipPar (Flow.scopeResource (connectionTo log "orders")) (Flow.scopeResource (connectionTo log "customers"))

                return! closedIn log
            }
            |> Flow.scoped

        let! afterScope = closedIn log
        return whileInUse, afterScope
    }
```

```fsharp run
openBoth |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success(0, 2))
```

Both connections remain open while `openBoth` uses them and close with its scope.

### Acquire in the background and keep it

To start acquiring one resource, do other work, and use the resource later, use `Flow.forkResource`. The acquisition
runs in a fiber, but the release belongs to the caller's scope, so `Fiber.join` returns a live resource. A plain
`Flow.fork` of the same acquisition returns a resource that the fiber has already released:

```fsharp
/// A plain fork: the fiber owns the connection and releases it when it settles, before join returns it.
let forkedConnectionClosedOnJoin : Flow<ClockEnvironment, string, bool> =
    flow {
        let! log = Ref.make []
        let! fiber = Flow.scopeResource (connectionTo log "audit") |> Flow.fork
        let! _ = Fiber.join fiber
        let! entries = Ref.get log
        return List.contains "close audit" entries
    }
    |> Flow.scoped

/// forkResource: the caller's scope owns the connection, so it is still open after join.
let keptConnection : Flow<ClockEnvironment, string, bool * string list> =
    flow {
        let! log = Ref.make []

        let! closedOnJoin =
            flow {
                let! connecting = Flow.forkResource (connectionTo log "audit")
                let! _ = Fiber.join connecting
                let! entries = Ref.get log
                return List.contains "close audit" entries
            }
            |> Flow.scoped

        let! entries = Ref.get log
        return closedOnJoin, entries
    }
```

```fsharp run
forkedConnectionClosedOnJoin |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success true)
keptConnection |> Flow.run (ClockEnvironment Clock.live) |> shouldEqual (Exit.Success(false, [ "open audit"; "close audit" ]))
```

If the caller's scope closes before the acquisition finishes, the fiber is interrupted, and a resource acquired anyway
is released straight away. [`Axial.Guardrails`](/notes/guardrails.html#axg007-keep-resources-acquired-by-a-fiber)
reports a forked flow that returns its own scoped resource as `AXG007`.

### Coming from ZIO or Effect

In ZIO 2 and Effect, a forked fiber shares its parent's `Scope`, so forking an `acquireRelease` and joining the fiber
returns a live resource. In Axial a forked fiber owns its scope, like `ZIO.scoped(effect).fork`; write
`Flow.forkResource` for that pattern.

`zipPar`, `race`, and the parallel traversals behave as in ZIO and Effect for values you receive. What a race loser or
a failed branch acquired is released as soon as Axial discards that flow, not when the scope closes. If the flow
stored the resource somewhere else first, such as in a `Ref`, that reference points at a released resource.

### Nested scopes

A fiber created with `Flow.fork` is itself registered with the current scope. Closing the scope interrupts and awaits an
unfinished fiber, then releases what the fiber acquired. A nested `Flow.scoped` gives resources and fibers a lifetime shorter than the surrounding application without passing
cleanup handles through every function.

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

Layers use the same model. `Layer.fromFlow` runs a flow in the layer's scope, so what it acquires with
`Flow.scopeAcquireRelease` stays alive until `Layer.provide` finishes; `Layer.acquireRelease` does the same for a
resource built by another layer.
