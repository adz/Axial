---
title: Effect-TS Comparison
description: How Axial and Effect-TS relate when an F# application targets .NET or JavaScript through Fable.
---

# Effect-TS Comparison

Axial and Effect-TS both model typed effects. Axial runs on .NET and through Fable, so an F# application can use the
same Flow model on server, Node, and browser targets without adopting Effect-TS for JavaScript deployment.

## Shared model

Both libraries provide concepts for:

- typed success and expected-error channels
- explicit services and dependency provisioning
- cold, compositional workflows
- cancellation and structured concurrency
- resource scopes and deterministic cleanup
- retries, schedules, and timeouts
- streams
- runtime observability

The names and exact semantics differ. This list describes the areas of overlap, not API compatibility.

## Why use Axial from F#

Axial keeps the workflow and domain model in F#:

- `flow { }` uses F# computation-expression syntax.
- `Flow<'env, 'error, 'value>` exposes dependencies, expected errors, and success in an F# type.
- `Result` and `Async` bind directly on .NET and Fable; .NET targets bind `ColdTask` for cold, cancellation-aware task factories. Raw `Task` and `ValueTask` values require explicit started-work adapters.
- On Fable, `Async` is the Flow-facing bridge for JavaScript asynchronous work. Promise-returning APIs can be adapted
  to `Async` and then bound in `flow { }`; application code does not need Effect-TS merely to await a Promise.
- Environment records and layers use ordinary F# values rather than a JavaScript-facing context boundary.
- `FlowStream` uses the same environment, error, cancellation, and runtime model on .NET and Fable.
- Node and browser hosting packages connect Flow interruption to platform lifecycle signals.
- Fable telemetry integrates with the JavaScript observability platform without changing the application workflow.

Shared F# code can retain Axial's typed effects, concurrency, streams, and observability across the
generated-JavaScript boundary without translating workflows into Effect-TS.

The concrete Promise adapter is Fable's `Async.AwaitPromise`; because `flow { }` binds `Async` directly, an adapted
Promise is one `let!` away:

```fsharp no-check reason="Compiles only with Fable; Async.AwaitPromise is the Fable.Core Promise bridge"
open Fable.Core

let awaitPromise (promise: JS.Promise<'T>) : Async<'T> =
    Async.AwaitPromise promise

let readRemote : Flow<unit, string, 'T> =
    flow {
        let! value = awaitPromise (loadRemote ())  // loadRemote returns JS.Promise<'T>
        return value
    }
```

## When Effect-TS is still the natural boundary

Use Effect-TS when the workflow is owned by TypeScript or must compose directly with an existing Effect-TS
application and its services. Calling that application from generated F# may be a legitimate integration boundary.
The JavaScript deployment target alone does not require Effect-TS as the runtime for F# code.

Use Axial when F# owns the application workflow and the code needs to run on .NET, Node, the browser, or a combination
of those targets. The same `Flow` API then remains the application model rather than an adapter on the way to a second
effect system.

## Compare concrete requirements

Compare the application's requirements:

- which language owns orchestration and service definitions
- whether workflows are shared between .NET and Fable
- which platform APIs require adapters
- which concurrency and stream operators the application actually uses
- which telemetry backend and hosting lifecycle own execution

For an F#-owned application, use Axial on every target its packages support: `Axial`, `Axial.Layers`, and the
operational packages run on .NET, Node, and browser, while `Axial.Hosting` is .NET-only and the Node/browser host
adapters are JavaScript-only. See [Packages and platforms](/notes/packages-and-platforms.html) for the matrix before
choosing a target. Introduce an Effect-TS boundary only when the application must participate in TypeScript code
that already uses Effect-TS.
