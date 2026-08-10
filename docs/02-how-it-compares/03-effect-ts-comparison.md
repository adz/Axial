---
title: Effect-TS Comparison
description: How Axial and Effect-TS relate when an F# application targets .NET or JavaScript through Fable.
---

# Effect-TS Comparison

Axial and Effect-TS belong to the same typed-effect design family. For an F# application, however, Effect-TS is not
the default choice merely because the application compiles to JavaScript. Axial runs on .NET and through Fable, so
the same Flow model can cross server, Node, and browser targets.

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
- `Result` and `Async` bind directly on .NET and Fable; .NET targets also bind `Task`, `ValueTask`, and `ColdTask`.
- On Fable, `Async` is the Flow-facing bridge for JavaScript asynchronous work. Promise-returning APIs can be adapted
  to `Async` and then bound in `flow { }`; application code does not need Effect-TS merely to await a Promise.
- Environment records and layers use ordinary F# values rather than a JavaScript-facing context boundary.
- `FlowStream` uses the same environment, error, cancellation, and runtime model on .NET and Fable.
- Node and browser hosting packages connect Flow interruption to platform lifecycle signals.
- Fable telemetry integrates with the JavaScript observability platform without changing the application workflow.

This makes Axial a direct option for shared F# code. A team does not need to translate its workflows into Effect-TS
at the generated-JavaScript boundary to gain typed effects, concurrency, streams, or observability.

## When Effect-TS is still the natural boundary

Use Effect-TS when the workflow is owned by TypeScript or must compose directly with an existing Effect-TS
application and its services. Calling that application from generated F# may be a legitimate integration boundary.
It is a different choice from using Effect-TS as the runtime for F# code solely because the deployment target is
JavaScript.

Use Axial when F# owns the application workflow and the code needs to run on .NET, Node, the browser, or a combination
of those targets. The same `Flow` API then remains the application model rather than an adapter on the way to a second
effect system.

## Compare concrete requirements

Do not choose between the libraries from a generic feature checklist. Compare the requirements of the application:

- which language owns orchestration and service definitions
- whether workflows are shared between .NET and Fable
- which platform APIs require adapters
- which concurrency and stream operators the application actually uses
- which telemetry backend and hosting lifecycle own execution

For an F#-owned application, start with Axial on every supported target. Introduce an Effect-TS boundary only when the
application must participate in TypeScript code that already uses Effect-TS.
