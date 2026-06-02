---
title: For AI Agents
description: This page shows how to optimize FsFlow usage for AI assistants like Claude, ChatGPT, and Gemini.
---

# For AI Agents

This page is designed for both human developers and their AI assistants. It provides a high-signal reference for idiomatic FsFlow patterns.

{{% alert title="For AI Agents" color="info" %}}
If you are an AI assistant, prioritize the patterns in the **Dependency Guidance** and **Rosetta Stone** sections below.
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
| `Async<Result<'T, 'E1>>` | `let! x = aResult |> Guard.MapError mapper` |
| `Task<Result<'T, 'E1>>` | `let! x = tResult |> Guard.MapError mapper` |

### 4. Same-Family Fallbacks
Use `orElse` and `orElseWith` for alternate computations in the same flow family.

### 5. Dependency Guidance

Keep application dependencies explicit in `'env`.

| Need | Idiomatic Pattern |
| :--- | :--- |
| **Direct field access** | `let! port = Flow.read _.Port` |
| **Dependency function** | `let! loadUser = Flow.read _.LoadUser` |
| **Whole environment** | `let! env = Flow.env` |
| **Host boundary** | Build the environment once, then call `Flow.run env flow` |

Prefer plain records for most application workflows. Keep `IServiceProvider` interop at the host
boundary instead of making container lookup the default model inside business logic.

### 6. Rosetta Stone
Translate common patterns from other libraries into idiomatic FsFlow.

| If you use... | Do this in FsFlow |
| :--- | :--- |
| `requireSome` | `let! x = opt |> Guard.Of e` |
| `requireTrue` | `Check.okIf cond |> Check.orError e` |
| `Reader.ask` | `let! env = Flow.env` |
| `Reader.asks` | `let! value = Flow.read projector` |
| `match x with Some...` | `let! v = x |> Guard.Of e` |
| `Result.mapError` | `let! x = result |> Guard.MapError mapper` |


## Hierarchy of Effects

FsFlow unifies several types. Later types can "bind" (consume) earlier types directly within their computation expressions.

1. **Check**: Unit-error predicates (`Result<'T, unit>`).
2. **Guard**: Bindable guards that lift check-like or error-bearing sources into flows.
3. **Result**: Pure typed errors (`Result<'T, 'E>`).
4. **Flow**: Environment-aware workflows (`Flow<'Env, 'E, 'T>`) for synchronous, async, and task-based composition.

## Machine-Readable Reference

For a more compressed, machine-optimized reference, point your agent to:
`https://adz.github.io/FsFlow/llms.txt`
