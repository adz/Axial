---
weight: 20
title: Validation & Results
type: docs
description: Overview of standard F# Result, FsFlow checks, fail-fast Result workflows, and accumulating validation.
---

# Validation & Results

This page shows how FsFlow starts from standard F# `Result<'value, 'error>` and layers on `Check`, `result {}`, `Validation`, and `validate {}` without making pure validation depend on `Flow`.

Use this section when your code is still pure. `Flow` is for application boundaries that need an explicit environment, async or task work, cancellation, resources, or runtime policy. The validation stack here can be used on its own as a small standalone layer.

## Start With F# Result

`Result<'value, 'error>` is the base fail-fast type in F#.

```fsharp
Ok value
Error problem
```

The standard library gives you the core combinators:

- `Result.map` changes the success value
- `Result.bind` chains the next result-producing step only after success
- `Result.mapError` changes the error value

Use them whenever one step has already produced a result:

```fsharp
let parseInt text =
    match System.Int32.TryParse text with
    | true, value -> Ok value
    | false, _ -> Error "not an int"

let parsedPlusOne =
    parseInt "41"
    |> Result.map ((+) 1)

let reciprocal text =
    parseInt text
    |> Result.bind (fun value ->
        if value = 0 then Error "zero"
        else Ok (1.0 / float value))
```

## What FsFlow Adds

FsFlow keeps the standard `Result` model and makes the validation story more structured:

| Layer | Shape | What it adds |
| --- | --- | --- |
| `Result<'value, 'error>` | standard F# carrier | fail-fast composition with `map`, `bind`, and `mapError` |
| `Check<'value>` | `Result<'value, unit>`-based helpers | reusable predicates, value-preserving gates, and extraction helpers |
| `result {}` | `Result` computation expression | clearer fail-fast workflows over ordinary `Result` |
| `Validation<'value, 'error>` | `Result<'value, Diagnostics<'error>>`-like carrier | accumulates independent failures instead of stopping at the first one |
| `validate {}` | validation computation expression | applicative accumulation with path-aware diagnostics |

`Check` and `result {}` are based directly on standard `Result`. `Validation` is Result-like, but its error side is expanded into `Diagnostics<'error>` so sibling failures can accumulate instead of stopping at the first one.

## How The Stack Fits

Keep the smallest honest shape for the problem:

```text
Check -> Result -> Validation
```

Use `Flow` only after the boundary grows and you need an explicit environment, async or task work, cancellation, resources, or runtime policy:

```text
Check -> Result -> Validation -> Flow
```

That separation keeps pure domain validation testable and reusable. A validation function can stay as `Result<string, RegistrationError>` today and later be bound inside `flow {}` without changing its core logic.

## What To Read Next

- [Check](./checks/): the predicate, preserving, and extracting helper shapes.
- [Result CE](./result-ce/): fail-fast composition over standard `Result`.
- [Validate CE](./validate-ce/): accumulating validation over `Diagnostics`.
- [Diagnostics](./diagnostics/): the structured error graph.
- [BindError](./bind-error/): the `flow {}`-edge adapter. It is not a general pure-`Result` helper.

## First Tutorial

Start with [Check, Result, and Validation](./check-result-tutorial/). It stays in pure code and builds a validation function in stages: first with `Check`, then with typed `Result`, then with `validate {}` when sibling failures should accumulate.
