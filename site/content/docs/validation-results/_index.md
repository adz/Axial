---
weight: 20
title: Validation & Results
type: docs
description: Overview of standard F# Result, FsFlow checks, fail-fast Result workflows, and accumulating validation.
---


This page shows how FsFlow builds on the standard F# `Result<'value, 'error>` type without making validation depend on `Flow`.

Start here when your code is still pure. `Flow` is for application boundaries that need an explicit environment, async or task work, cancellation, resources, or runtime policy. Checks, `Result`, and `Validation` can be useful as a small standalone validation layer.

## Start With F# Result

`Result<'value, 'error>` is the base fail-fast type in F#:

```fsharp
Ok value
Error problem
```

Use the standard library functions when one step has already produced a result:

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

`Result.map` changes a success value. `Result.bind` runs the next result-producing step only after success. `Result.mapError` changes the error value.

## What FsFlow Adds

FsFlow keeps those standard `Result` semantics and adds a small stack around them:

1. **[Check](./checks/)**: pure predicates, preserving gates, and extraction helpers under one module.
2. **[Result CE](./result-ce/)**: `result {}` syntax for fail-fast chains of standard `Result<'value, 'error>`.
3. **[Validate CE](./validate-ce/)**: `validate {}` syntax for accumulating independent failures.
4. **[Diagnostics](./diagnostics/)**: a path-aware diagnostics graph used by accumulating validation.
5. **[BindError](./bind-error/)**: a `flow {}` bind-site adapter. Use it at the Flow boundary, not as a general Result helper.

`Check` and `result {}` are based directly on standard `Result`. `Validation` is Result-like, but its error side is expanded into `Diagnostics<'error>` so independent failures can accumulate instead of stopping at the first one.

## Check Once, Lift Later

Keep the smallest honest shape:

```text
Check -> Result -> Validation
```

Use `Flow` only after the boundary grows:

```text
Check -> Result -> Validation -> Flow
```

That separation keeps pure domain validation testable and reusable. A validation function can stay as `Result<string, RegistrationError>` today and later be bound inside `flow {}` without changing its core logic.

## First Tutorial

Start with [Check to Result](./check-result-tutorial/). It builds a pure validation function in stages: first with `Check`, then with typed errors, then with `result {}`.
