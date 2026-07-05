---
weight: 70
title: Comparisons and Integrations
description: Other libraries Axial compares with and works alongside, including FsToolkit, Validus, FSharpPlus, and Effect-TS.
type: docs
---



This page compares Axial with libraries you may already use, and shows where they fit together in the same codebase.

The rule of thumb is simple: keep each library on the boundary it already owns, then let Axial take over orchestration where the runtime shape becomes explicit.

## How The Libraries Differ

- `FsToolkit.ErrorHandling` is the established result-oriented layer: it uses core F# types, a small number of wrappers such as `Task<Result<_,_>>`, and a familiar module-and-builder surface. It fits existing code with low overhead.
- `Validus` is a richer validation DSL if you already use it. Axial now covers the common check/result/validation path itself.
- `FSharpPlus` is the generic FP layer: it brings broad abstractions and monad-transformer-style composition, but that also means more compiler work and more complex error surfacing when you are trying to follow a Axial boundary.

## What Axial Adds

Axial captures the common application boundary needs in one model:

- Check for reusable predicates
- Result for fail-fast typed failures
- Validation for structured accumulation
- Flow for synchronous boundaries
- Flow for async boundaries
- Flow for task boundaries
- `'env` / `'ctx` style context threading for implicit dependencies, request metadata, and other runtime state

That gives you a smaller surface area when the application boundary is what you want to make explicit.

## Examples To Read Next

The runnable examples page includes two on-point scenarios:

- a request boundary example that pulls a user from environment-provided data and threads a trace id through the boundary
- a task-shaped example that keeps cold task work delayed until the boundary runs

Read [`Runnable Examples`](../patterns/examples/) after this page if you want to see those patterns in executable form.

## Replacing FsToolkit.ErrorHandling

Use `FsToolkit.ErrorHandling` when you already have Result, `AsyncResult`, or `TaskResult` code in production.

This is the closest migration path for existing railway-oriented code:

- keep pure validation and mapping code as plain Result
- move the orchestration boundary into Flow and bind async/task work directly where needed
- use Check when the check itself can stay pure and only the final error provisioning becomes effectful

Go to [`Replacing FsToolkit.ErrorHandling`](./integrations-fstoolkit/) for the migration shape and coexistence patterns.

## FluentValidation Comparison

FluentValidation validates objects that already exist; Axial parses input into objects that cannot exist in an
invalid state, and its constraints are inspectable metadata rather than lambdas inside validator classes.

Go to [`vs FluentValidation`](./fluentvalidation-comparison/) for the model comparison.

## zod Comparison

Axial's schema group is the same parse-don't-validate philosophy zod made mainstream in TypeScript — declared
against your own types, with no reflection, NativeAOT and trimming safety, and Fable support.

Go to [`vs zod`](./zod-comparison/) for the mapping between the two.

## Validus Integration

Use `Validus` when your codebase already has richer input validation rules or value-object style guards.

The best coexistence pattern is:

- validate the incoming model with `Validus`
- keep the result pure
- bridge the final Result into Axial when the runtime boundary begins

Go to [`Validus Integration`](./integrations-validus/) for the integration shape and examples.

## FSharpPlus Integration

Use `FSharpPlus` when the codebase already depends on broad functional helpers and generic FP abstractions.

Axial can sit beside that style. Instead:

- keep Axial at the orchestration boundary
- continue using FSharpPlus for the generic transformations your codebase already relies on
- avoid mixing too many abstraction layers inside a single step

Go to [`FSharpPlus Integration`](./integrations-fsharpplus/) for the coexistence guidance.
