---
weight: 70
title: Ecosystem Overview
description: How FsFlow fits beside FsToolkit, Validus, IcedTasks, and FSharpPlus.
---


# Ecosystem Overview

This page shows how FsFlow can live alongside the libraries you are most likely to already have in a codebase.

The rule of thumb is simple: keep each library on the boundary it already owns, then let FsFlow take over orchestration where the runtime shape becomes explicit.

## How The Libraries Differ

- `FsToolkit.ErrorHandling` is the established result-oriented layer: it uses core F# types, a small number of wrappers such as `Task<Result<_,_>>`, and a familiar module-and-builder surface. It fits existing code with low overhead.
- `Validus` is a richer validation DSL if you already use it. FsFlow now covers the common check/result/validation path itself.
- `IcedTasks` is the task-shape layer: it is interesting for performance and task-native ergonomics, but it still lives in the `Async` / `Task` / Result space rather than in a richer environment model.
- `FSharpPlus` is the generic FP layer: it brings broad abstractions and monad-transformer-style composition, but that also means more compiler work and more complex error surfacing when you are trying to follow a FsFlow boundary.

## What FsFlow Adds

FsFlow captures the common application boundary needs in one model:

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

Read [`Runnable Examples`](./examples/README.md) after this page if you want to see those patterns in executable form.

## Replacing FsToolkit.ErrorHandling

Use `FsToolkit.ErrorHandling` when you already have Result, `AsyncResult`, or `TaskResult` code in production.

This is the closest migration path for existing railway-oriented code:

- keep pure validation and mapping code as plain Result
- move the orchestration boundary into Flow and bind async/task work directly where needed
- use Check when the check itself can stay pure and only the final error provisioning becomes effectful

Go to [`Replacing FsToolkit.ErrorHandling`](./INTEGRATIONS_FSTOOLKIT.md) for the migration shape and coexistence patterns.

## Validus Integration

Use `Validus` when your codebase already has richer input validation rules or value-object style guards.

The best coexistence pattern is:

- validate the incoming model with `Validus`
- keep the result pure
- bridge the final Result into FsFlow when the runtime boundary begins

Go to [`Validus Integration`](./INTEGRATIONS_VALIDUS.md) for the integration shape and examples.

## IcedTasks Integration

Use `IcedTasks` when the codebase already thinks in task-centric computation expressions, especially ColdTask and cancellable task shapes.

FsFlow fits beside it when you want:

- typed failure values
- explicit environment threading
- a model that still understands task-native boundaries

Go to [`IcedTasks Integration`](./INTEGRATIONS_ICEDTASKS.md) for the task-shape comparison.

## FSharpPlus Integration

Use `FSharpPlus` when the codebase already depends on broad functional helpers and generic FP abstractions.

FsFlow can sit beside that style. Instead:

- keep FsFlow at the orchestration boundary
- continue using FSharpPlus for the generic transformations your codebase already relies on
- avoid mixing too many abstraction layers inside a single step

Go to [`FSharpPlus Integration`](./INTEGRATIONS_FSHARPPLUS.md) for the coexistence guidance.

## Choosing Quickly

Use:

- `FsToolkit.ErrorHandling` when you are migrating existing `Async<Result<_,_>>` or `TaskResult` code
- `Validus` when you already have validation rules and want to keep them pure
- `IcedTasks` when task shape and cancellation-aware cold tasks are already part of the codebase
- `FSharpPlus` when the codebase already leans on a general FP base library and you want FsFlow to stay focused on orchestration

## Next

Read the library-specific pages for concrete coexistence and migration patterns:

- [`Replacing FsToolkit.ErrorHandling`](./INTEGRATIONS_FSTOOLKIT.md)
- [`Validus Integration`](./INTEGRATIONS_VALIDUS.md)
- [`IcedTasks Integration`](./INTEGRATIONS_ICEDTASKS.md)
- [`FSharpPlus Integration`](./INTEGRATIONS_FSHARPPLUS.md)
