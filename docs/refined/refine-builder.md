---
weight: 10
title: Refine CE
description: Sequencing parsing and refinement with the refine { } builder.
---

# Refine CE

Use the `refine {}` computation expression when you need to parse and refine multiple input fields into a validated domain record in a fail-fast manner.

Inside `refine {}`, you can bind standard F# `Result` values, parse operations, and refinement helpers. It aggregates these operations under `RefinementError`.

## Basic Usage

The `refine {}` builder is optimized for constructing type-safe domain models at boundary points:

```fsharp
open Axial
open Axial.Refined

type UserId = UserId of PositiveInt
type Email = Email of NonBlankString

type User = { Id: UserId; Email: Email }

let createUser (rawId: string) (rawEmail: string) : Result<User, RefinementError> =
    refine {
        let! parsedId = Parse.int rawId
        let! positiveId = Refine.positiveInt parsedId
        let! email = Refine.nonBlankString rawEmail
        
        return {
            Id = UserId positiveId
            Email = Email email
        }
    }
```

## Parsing Helpers

`Parse` contains pure functions that parse raw inputs (usually string representations) and return `Result<'value, RefinementError>` or optional options:

- `Parse.int`, `Parse.bool`, `Parse.decimal`, `Parse.float`
- `Parse.guid`, `Parse.dateTime`, `Parse.dateTimeOffset`, `Parse.dateOnly`, `Parse.timeOnly`
- `Parse.enum`

On failure, these helpers return a detailed `RefinementError` indicating the target type and input value.

## Refinement Helpers

`Refine` validates that a value satisfies specific rules, wrapping it in a refined type wrapper on success:

- `Refine.nonBlankString`: returns a `NonBlankString`
- `Refine.positiveInt`: returns a `PositiveInt`
- `Refine.nonEmptyList`: returns a `NonEmptyList`

## When to use `refine {}`

- **Parsing Strings**: When reading values from query strings, environment variables, or CLI inputs.
- **Fail-Fast Boundary Validation**: When constructing values where the type system itself guarantees the invariant (e.g. you cannot have a user record with an empty email).

If you need to collect multiple independent errors across fields without failing fast, use [`validate {}`](../validation/validate-builder/) instead.
