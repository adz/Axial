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
Use `Check` for pure validation. Unprefixed helpers are yes/no predicates, `when*` helpers preserve the original value, `take*` helpers extract an inner value or return a deliberately different success shape, and `Check.withError` turns a pure unit-error result into a domain error.

| Source Type | Idiomatic Pattern |
| :--- | :--- |
| `bool` | `Check.isTrue condition |> Check.withError e` |
| `string` value | `name |> Check.whenNotBlank |> Check.withError e` |
| `option<'T>` | `opt |> Check.takeSome |> Check.withError e` |
| `voption<'T>` | `vopt |> Check.takeValueSome |> Check.withError e` |
| `Result<'T, unit>` | `check |> Check.withError e` |

### 2. Binding Error-Adapted Sources (Idiomatic Way)
Use `BindError.withError` inside `flow {}` when the source fails with option/value-option absence or a `unit` error, and you need to assign the flow's domain error at the bind site.

| Source Type | Idiomatic Pattern |
| :--- | :--- |
| `Option<'T>` | `let! x = opt |> BindError.withError e` |
| `voption<'T>` | `let! x = vopt |> BindError.withError e` |
| `Async<Option<'T>>` | `let! x = aOpt |> BindError.withError e` |
| `Async<voption<'T>>` | `let! x = aVOpt |> BindError.withError e` |
| `bool` predicate | `do! cond |> Check.isTrue |> BindError.withError e` |
| `Result<'T, unit>` | `let! x = check |> BindError.withError e` |
| `Flow<'Env, unit, 'T>` | `let! x = flow |> BindError.withError e` |
| `Task<Option<'T>>` | `let! x = tOpt |> BindError.withError e` |
| `Task<voption<'T>>` | `let! x = tVOpt |> BindError.withError e` |

### 3. Mapping Errors (Idiomatic Way)
Use `BindError.map` inside `flow {}` when the source already carries a meaningful error value that must be wrapped or translated before binding.

| Source Type | Idiomatic Pattern |
| :--- | :--- |
| `Result<'T, 'E1>` | `let! x = result |> BindError.map mapper` |
| `Flow<'Env, 'E1, 'T>` | `let! x = flow |> BindError.map mapper` |
| `Async<Result<'T, 'E1>>` | `let! x = aResult |> BindError.map mapper` |
| `Task<Result<'T, 'E1>>` | `let! x = tResult |> BindError.map mapper` |

### 4. Same-Family Fallbacks
Use `orElse` and `orElseWith` for alternate computations in the same flow family.

### 5. Dependency Guidance

Keep application dependencies explicit in `'env`.

| Need | Idiomatic Pattern |
| :--- | :--- |
| **Direct field access** | `let! port = Flow.read _.Port` |
| **Dependency function** | `let! loadUser = Flow.read _.LoadUser` |
| **Named service** | `let! clock = Service<IClock>.get()` |
| **Whole environment** | `let! env = Flow.env` |
| **Provisioned environment** | `flow |> Flow.provide appLayer` |
| **Host boundary** | Build the environment once, or use `Service<'T>.resolve()` only in edge glue |

Prefer plain records for most application workflows. Keep `IServiceProvider` interop at the host
boundary instead of making container lookup the default model inside business logic. Use layers to
validate provider-backed services and build reusable explicit environments.

### 6. Rosetta Stone
Translate common patterns from other libraries into idiomatic FsFlow.

| If you use... | Do this in FsFlow |
| :--- | :--- |
| `requireSome` | `let! x = opt |> BindError.withError e` in `flow {}` or `opt |> Check.takeSome |> Check.withError e` in pure code |
| `requireTrue` | `cond |> Check.isTrue |> Check.withError e` |
| `Reader.ask` | `let! env = Flow.env` |
| `Reader.asks` | `let! value = Flow.read projector` |
| `ZIO.service` | `let! service = Service<IService>.get()` |
| `.NET IServiceProvider.GetRequiredService` | `let! service = Service<IService>.resolve()` at the edge |
| `match x with Some...` | `let! v = x |> BindError.withError e` in `flow {}` |
| `Result.mapError` | `let! x = result |> BindError.map mapper` in `flow {}` |


## Hierarchy of Effects

FsFlow unifies several types. Later types can "bind" (consume) earlier types directly within their computation expressions.

1. **Check**: Unit-error predicates (`Result<'T, unit>`).
2. **Result**: Pure typed errors (`Result<'T, 'E>`).
3. **Validation**: Accumulating diagnostics.
4. **Flow**: Environment-aware workflows (`Flow<'Env, 'E, 'T>`) for synchronous, async, and task-based composition.

## Machine-Readable Reference

For a more compressed, machine-optimized reference, point your agent to:
`llms.txt` in the repo root, or the locally generated preview at `/llms.txt` when serving the docs site.
