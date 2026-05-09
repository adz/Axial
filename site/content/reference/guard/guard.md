---
title: Guard
description: Source-documented bindable guard constructors for FsFlow.
type: docs
---

# Guard

This page shows the source-documented `Guard` surface: the constructors that take a
source value plus an error and turn them into a bindable `Result`, `Validation`, or
flow value.

## Core type

- type `Guard`: Constructors for turning predicate-like and error-bearing sources into bindable results and flows. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Guard.fs#L5)

## Constructors

- `Guard.Of`: Bridges `bool`, `option`, `voption`, `Result<'value, unit>`, `Validation<'value, unit>`,
  `Async<bool>`, `Async<option<'value>>`, `Async<voption<'value>>`, `Task<bool>`, `Task<option<'value>>`,
  `Task<voption<'value>>`, `ValueTask<bool>`, `ValueTask<option<'value>>`, `ValueTask<voption<'value>>`,
  `Flow<'env, unit, 'value>`, `AsyncFlow<'env, unit, 'value>`, and `TaskFlow<'env, unit, 'value>` into
  typed errors. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Guard.fs#L8)
- `Guard.MapError`: Remaps the error of `Result<'value, 'error1>`, `Validation<'value, 'error1>`,
  `Async<Result<'value, 'error1>>`, `Task<Result<'value, 'error1>>`, `ValueTask<Result<'value, 'error1>>`,
  `Flow<'env, 'error1, 'value>`, `AsyncFlow<'env, 'error1, 'value>`, and
  `TaskFlow<'env, 'error1, 'value>` into a new error type. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Guard.fs#L127)

## When To Use It

- Use `Guard.Of` when the source is already a boolean or predicate-shaped value and you want the CE to bind
  the source while preserving the supplied error.
- Use `Guard.MapError` when the source already carries a meaningful error value and you want to keep the same
  source shape while changing that error type.
- Keep `Check` for reusable pure predicates. Use `Check.orError` when the predicate is already pure and you just
  need to attach a domain error.

## Examples

```fsharp
open FsFlow

let validateName name =
    Check.notBlank name |> Check.orError "Name required"

let login username password =
    asyncFlow {
        let! user = tryGetUser username |> Guard.MapError Unauthorized
        do! Check.notBlank password |> Guard.Of InvalidPassword
        return user
    }

let validateProfile profile =
    validate {
        let! name = profile.Name |> Guard.Of NameRequired
        let! email = profile.Email |> Check.notBlank |> Check.orError EmailRequired
        return name, email
    }
```
