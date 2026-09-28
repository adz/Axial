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

## Fibers and nested work

A fiber created with `Flow.fork` belongs to the current scope. Closing the scope interrupts and awaits an unfinished
fiber. A nested `Flow.scoped` therefore gives a group of resources and fibers a lifetime shorter than the surrounding
application without requiring every function to pass cleanup handles manually.

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
- Nested scopes close before their parent.
- Finalizers run in reverse registration order and at most once.
- Cleanup failures are defects and combine with the failure that caused closure.

Layers use the same model. `Layer.acquireRelease` keeps a provisioned service alive until `Layer.provide` finishes.
