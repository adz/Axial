---
weight: 10
title: Refine CE
description: Sequencing parsing and refinement with the refine { } builder.
---

# Refine CE

Use `refine {}` to connect parsing and refinement steps. Each successful step passes its value to the next line, and
the block stops at the first error.

## What the keywords do

Suppose the block calls these functions:

```fsharp
let parseInt (text: string) : Result<int, ParseError> = Parse.int text
let checkAllowed (id: PositiveInt) : Result<unit, RefinementError> = ...
let createAccount (id: PositiveInt) : Result<Account, RefinementError> = ...
```

`let!` binds the successful value to the name on its left. `do!` binds a step whose successful value is `unit`.
`return!` uses another refinement result as the result of the block.

```fsharp
refine {
    let! parsed = Parse.int rawId
    let! id = Refine.positiveInt parsed
    do! checkAllowed id
    return! createAccount id
}
```

Here is the same block with the left- and right-hand types shown:

```fsharp
refine {
    let! (parsed: int) =
        (Parse.int rawId: Result<int, ParseError>)

    let! (id: PositiveInt) =
        (Refine.positiveInt parsed: Result<PositiveInt, RefinementError>)

    do! (checkAllowed id: Result<unit, RefinementError>)
    return! (createAccount id: Result<Account, RefinementError>)
}
// Result<Account, RefinementError>
```

A parse error becomes `RefinementError.ParseFailed`. Other bound results use `RefinementError` directly.

## Basic Usage

This example builds a domain value from two strings:

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

If you need to collect multiple independent errors across fields without failing fast, use [`validate {}`]({{< relref "/error-handling/diagnostics/validate-builder.md" >}}) instead.
