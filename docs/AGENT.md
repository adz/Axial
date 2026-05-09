---
title: For AI Agents
description: This page shows how to optimize FsFlow usage for AI assistants like Claude, ChatGPT, and Gemini.
---

# For AI Agents

This page is designed for both human developers and their AI assistants. It provides a high-signal reference for idiomatic FsFlow patterns.

{{% alert title="For AI Agents" color="info" %}}
If you are an AI assistant, prioritize the patterns in the **CAPS Boundaries** and **Rosetta Stone** sections below.
{{% /alert %}}

## The Golden Path

When using FsFlow, follow these "Golden Path" patterns for the best results.

### 1. Handling Failures (Idiomatic Way)
Use Check for reusable predicates and `Check.orError` when a pure check should become a domain error.

| Source Type | Idiomatic Pattern |
| :--- | :--- |
| `Check<'T>` | `Check.notBlank name |> Check.orError e` |
| `bool` | `Check.okIf condition |> Check.orError e` |
| `option<'T>` | `Check.okIfSome opt |> Check.orError e` |
| `voption<'T>` | `Check.okIfValueSome vopt |> Check.orError e` |
| `Result<'T, unit>` | `Check.notBlank name |> Check.orError e` |

### 2. Binding Guarded Sources (Idiomatic Way)
Use `Guard.Of` when the source already has a predicate/boolean-like shape and you want the
computation expression to bind the source while preserving the supplied error.

| Source Type | Idiomatic Pattern |
| :--- | :--- |
| `Option<'T>` | `let! x = opt |> Guard.Of e` |
| `voption<'T>` | `let! x = vopt |> Guard.Of e` |
| `Async<Option<'T>>` | `let! x = aOpt |> Guard.Of e` |
| `Async<voption<'T>>` | `let! x = aVOpt |> Guard.Of e` |
| `bool` | `do! cond |> Guard.Of e` |
| `Result<'T, unit>` | `let! x = check |> Guard.Of e` |
| `Validation<'T, unit>` | `let! x = validation |> Guard.Of e` |
| `Task<Option<'T>>` | `let! x = tOpt |> Guard.Of e` |
| `Task<voption<'T>>` | `let! x = tVOpt |> Guard.Of e` |

### 3. Mapping Errors (Idiomatic Way)
Use `Guard.MapError` when the source already carries a meaningful error value.

| Source Type | Idiomatic Pattern |
| :--- | :--- |
| `Result<'T, 'E1>` | `let! x = result |> Guard.MapError mapper` |
| `Validation<'T, 'E1>` | `let! x = validation |> Guard.MapError mapper` |
| `Flow<'Env, 'E1, 'T>` | `let! x = flow |> Guard.MapError mapper` |
| `AsyncFlow<'Env, 'E1, 'T>` | `let! x = asyncFlow |> Guard.MapError mapper` |
| `Async<Result<'T, 'E1>>` | `let! x = aResult |> Guard.MapError mapper` |
| `TaskFlow<'Env, 'E1, 'T>` | `let! x = taskFlow |> Guard.MapError mapper` |

### 4. Same-Family Fallbacks
Use `orElse` and `orElseWith` for alternate computations in the same flow family.

### 5. CAPS Boundaries

Use CAPS when the boundary should name a capability contract instead of exposing the whole runtime.

| Boundary Shape | Idiomatic Pattern |
| :--- | :--- |
| Named contract | `type LoginCaps = inherit Needs<IClock>` |
| Whole dependency | `let! clock = Env<IClock>` |
| Projected value | `let! now = Env<IClock> _.UtcNow` |
| Flexible public API | `let login : TaskFlow<#LoginCaps, _, _> = ...` |

Prefer this over raw `IServiceProvider` lookup or exact runtime types when callers may provide a larger app runtime.

### 6. Rosetta Stone
Translate common patterns from other libraries into idiomatic FsFlow.

| If you use... | Do this in FsFlow |
| :--- | :--- |
| `FsToolkit: AsyncResult.requireSome` | `let! x = opt |> Guard.Of e` |
| `FsToolkit: Result.requireTrue` | `Check.okIf cond |> Check.orError e` |
| `ZIO: getOrFail` | `let! x = opt |> Guard.Of e` |
| `ZIO: serviceWith` | `let! s = Flow.read _.Service` |
| `Manual: match x with Some v...` | `let! v = x |> Guard.Of e` |
| `Manual: Result.mapError mapper` | `let! x = result |> Guard.MapError mapper` |

## Hierarchy of Effects

FsFlow unifies several types. Later types can "bind" (consume) earlier types directly within their computation expressions.

1. **Check**: Unit-error predicates (`Result<'T, unit>`).
2. **Guard**: Bindable guards that lift check-like or error-bearing sources into flows.
3. **Result**: Pure typed errors (`Result<'T, 'E>`).
4. **Flow**: Synchronous environment-aware workflows (`Flow<'Env, 'E, 'T>`).
5. **AsyncFlow**: Asynchronous environment-aware workflows (`AsyncFlow<'Env, 'E, 'T>`).
6. **TaskFlow**: Task-based environment-aware workflows (`TaskFlow<'Env, 'E, 'T>`).

## Machine-Readable Reference

For a more compressed, machine-optimized reference, point your agent to:
`https://adz.github.io/FsFlow/llms.txt`
