---
weight: 10
title: Refine CE
description: Sequencing parsing and refinement with the refine { } builder.
---

# Refine CE

This page shows how to use the `refine {}` computation expression when you need to parse and refine multiple input fields into a domain record in a fail-fast manner.

Inside `refine {}`, you can bind parse operations, refinement helpers, and standard F# `Result` values that use `RefinementError`. Parse failures are wrapped as `RefinementError.ParseFailed`.

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

`Parse` contains pure functions that parse untrusted values, usually string representations, and return `Result<'value, ParseError>`:

- `Parse.int`, `Parse.bool`, `Parse.decimal`, `Parse.float`
- `Parse.guid`, `Parse.dateTime`, `Parse.dateTimeOffset`, `Parse.dateOnly`, `Parse.timeOnly`
- `Parse.enum`

On failure, these helpers return a `ParseError` indicating the target type and input value. When bound inside `refine {}`, that failure becomes `RefinementError.ParseFailed`.

## Refinement Helpers

`Refine` validates that a value satisfies specific rules, wrapping it in a refined type wrapper on success:

- `Refine.nonBlankString`: returns a `NonBlankString`
- `Refine.trimmedString`: returns a `TrimmedString`
- `Refine.boundedString`: returns a `BoundedString`
- `Refine.slug`: returns a `Slug`
- `Refine.positiveInt`: returns a `PositiveInt`
- `Refine.nonNegativeInt`: returns a `NonNegativeInt`
- `Refine.nonZeroInt`: returns a `NonZeroInt`
- `Refine.nonEmptyList`: returns a `NonEmptyList`
- `Refine.nonEmptyArray`: returns a `NonEmptyArray`
- `Refine.distinctList`: returns a `DistinctList`
- `Refine.boundedList`: returns a `BoundedList`
- `Refine.boundedArray`: returns a `BoundedArray`
- `Refine.dateTimeOffsetRange`: returns a `DateTimeOffsetRange`

`refine {}` can also bind raw values directly when the target refined type is clear from inference:

```fsharp
let trustedName : Result<NonBlankString, RefinementError> =
    refine {
        let! name = "Ada"
        return name
    }
```

Use an explicit left-hand annotation when the target refined type would otherwise be unclear:

```fsharp
let trustedName =
    refine {
        let! (name: NonBlankString) = "Ada"
        return name
    }
```

## When to use `refine {}`

- **Parsing Strings**: When reading values from query strings, environment variables, or CLI inputs.
- **Fail-Fast Boundary Validation**: When constructing values where the type system itself guarantees the invariant (e.g. you cannot have a user record with an empty email).

If you need to collect multiple independent errors across fields without failing fast, use [`validate {}`]({{< relref "/error-handling/validation/validate-builder.md" >}}) instead.
