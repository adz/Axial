---
title: For AI Agents
description: This page shows Axial usage patterns for AI assistants.
---

# For AI Agents

This page lists Axial patterns that coding agents should follow.

{{% alert title="For AI Agents" color="info" %}}
If you are an AI assistant, prioritize the patterns in the **Dependency Guidance** and **Rosetta Stone** sections below.
{{% /alert %}}

## Default Patterns

Use these patterns unless local code shows a different convention.

**The two-lane rule comes first**: modelling a domain → declare a `Schema` and parse with `Input.parse`; simple code
without a domain model → plain `Result` with a user-owned error DU. `Check`, `Validation`, and `Refined` are machinery
behind those doors; `Flow` is the optional effects side.

For schema boundaries, use `SchemaError` as the one interpreter error shape. Lower subsystem failures with
`SchemaError.ofParseError`, `SchemaError.ofRefinementError`, or `SchemaError.ofCheckFailure`; render with
`SchemaError.render` or `ParsedInput.renderErrors`; map to application errors with `ParsedInput.mapErrors`.

### 1. Handling Failures
Use `Check` for executable value constraints, `Predicate` for local boolean tests, and `Result` for fail-fast values. `Check.*` helpers return `Result<unit, CheckFailure list>`; `Result.guard`, named `Result.*` guards, and extraction helpers preserve values or change the success shape.

| Source Type | Idiomatic Pattern |
| :--- | :--- |
| `bool` | `Result.checkOr e condition` |
| `string` value | `name |> Result.notBlank |> Result.mapError (fun _ -> e)` |
| `option<'T>` | `opt |> Result.someOr e` |
| `voption<'T>` | `vopt |> Result.valueSomeOr e` |
| check + value | `value |> Result.guard check |> Result.mapError mapper` |

### 2. Binding Error-Adapted Sources
Use `Bind.error` inside `flow {}` when the source fails with option/value-option absence or a `unit` error, and you need to assign the flow's domain error at the bind site.

| Source Type | Idiomatic Pattern |
| :--- | :--- |
| `Option<'T>` | `let! x = opt |> Bind.error e` |
| `voption<'T>` | `let! x = vopt |> Bind.error e` |
| `Async<Option<'T>>` | `let! x = aOpt |> Bind.error e` |
| `Async<voption<'T>>` | `let! x = aVOpt |> Bind.error e` |
| `bool` predicate | `do! Result.checkOr () cond |> Bind.error e` |
| `Result<'T, unit>` | `let! x = check |> Bind.error e` |
| `Flow<'Env, unit, 'T>` | `let! x = flow |> Bind.error e` |
| `Task<Option<'T>>` | `let! x = tOpt |> Bind.error e` |
| `Task<voption<'T>>` | `let! x = tVOpt |> Bind.error e` |

### 3. Mapping Errors
Use `Bind.mapError` inside `flow {}` when the source already carries a meaningful error value that must be wrapped or translated before binding.

| Source Type | Idiomatic Pattern |
| :--- | :--- |
| `Result<'T, 'E1>` | `let! x = result |> Bind.mapError mapper` |
| `Flow<'Env, 'E1, 'T>` | `let! x = flow |> Bind.mapError mapper` |
| `Async<Result<'T, 'E1>>` | `let! x = aResult |> Bind.mapError mapper` |
| `Task<Result<'T, 'E1>>` | `let! x = tResult |> Bind.mapError mapper` |

### 4. Same-Family Fallbacks
Use `orElse` and `orElseWith` for alternate computations in the same flow family.

### 5. Flow Signatures

Start with the smallest useful Flow signature. Expand to the full `Flow<'env, 'error, 'value>` form only when a workflow needs both environment and typed failure channels:

| Alias | Use when |
| :--- | :--- |
| `Flow<'value>` | No environment and no typed failure. |
| `Flow<'error, 'value>` | No environment, with typed failure. |
| `EnvFlow<'env, 'value>` | Environment, with no typed failure. |
| `ExnFlow<'value>` | No environment, with recoverable exceptions as typed failures. |
| `ExnEnvFlow<'env, 'value>` | Environment, with recoverable exceptions as typed failures. |

Use `Flow.fromAsync`, `Flow.fromTask`, and `Flow.fromValueTask` when thrown exceptions are defects. Use `Flow.attemptAsync`, `Flow.attemptTask`, and `Flow.attemptValueTask` when expected exceptions should become `Cause.Fail exn`.

### 6. Dependency Guidance

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

### 7. Rosetta Stone
Translate common patterns from other libraries into idiomatic Axial.

| If you use... | Do this in Axial |
| :--- | :--- |
| `requireSome` | `let! x = opt |> Bind.error e` in `flow {}` or `opt |> Result.someOr e` in pure code |
| `requireTrue` | `Result.checkOr e cond` |
| `Reader.ask` | `let! env = Flow.env` |
| `Reader.asks` | `let! value = Flow.read projector` |
| `ZIO.service` | `let! service = Service<IService>.get()` |
| `.NET IServiceProvider.GetRequiredService` | `let! service = Service<IService>.resolve()` at the edge |
| `match x with Some...` | `let! v = x |> Bind.error e` in `flow {}` |
| `Result.mapError` | `let! x = result |> Bind.mapError mapper` in `flow {}` |
| retry policy | `flow |> Schedule.retry schedule` |
| repeat policy | `flow |> Schedule.repeat schedule` |
| ActiveModel / FluentValidation validators | `Schema<'model>` + `Input.parse` — constraints declared once, invalid models never constructed |
| DTO + manual mapping into domain types | schema fields over refined value schemas (`Value.refined`) |
| form redisplay with per-field errors | `parsed.Input` + `RawInput.redisplayPath`, `parsed.ErrorsFor "contacts[1].value"` |
| workflow-specific business rules | `RuleSet` + `Rules.apply` over the already-trusted model |
| guard clauses at workflow entry | `Policy` + `Flow.verify` |


## Hierarchy of Effects

Later types can bind earlier types directly within their computation expressions.

1. **Check**: Executable value constraints (`'T -> Result<unit, CheckFailure list>`). Use `Predicate` for raw boolean tests.
2. **Result**: Fail-fast typed errors (`Result<'T, 'E>`).
3. **Refined**: Parsing and structural refined values.
4. **Validation**: Accumulating diagnostics.
5. **Schema**: Portable model metadata (`Schema<'model>`) interpreted by `Input.parse`, `Validation.validate`, `Rules.apply`, and `Inspect`.
6. **Flow**: Environment-aware workflows (`Flow<'Env, 'E, 'T>`) for synchronous, async, and task-based composition.

## Machine-Readable Reference

For a compact reference, point your agent to:
`llms.txt` in the repo root, or the locally generated preview at `/llms.txt` when serving the docs site.
