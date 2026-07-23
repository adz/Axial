---
weight: 10
title: Refine CE
description: Sequencing parsing and refinement with the refine { } builder.
---

# Refine CE

Use `refine {}` to parse and refine raw values from the type expected on the left side of `let!`. Each successful step
passes its value to the next line, and the block stops at the first error.

One value needs no computation expression:

```fsharp
let id : Result<int, RefinementError> =
    Refine.from rawId
```

`refine {}` sequences several dependent refinements and stops at the first failure.

## What the keywords do

`let!` selects a parser or refined constructor when its right side is raw input. `do!` binds a step whose successful
value is `unit`. `return!` uses another refinement result as the result of the block.

```fsharp
refine {
    let! (parsed: int) = rawId
    let! (id: PositiveInt) = parsed
    do! checkAllowed id
    return! createAccount id
}
```

Here is the same block with the right-hand types and result-returning steps shown explicitly:

```fsharp
refine {
    let! (parsed: int) = (rawId: string)
    let! (id: PositiveInt) = (parsed: int)
    do! (checkAllowed id: Result<unit, RefinementError>)
    return! (createAccount id: Result<Account, RefinementError>)
}
// Result<Account, RefinementError>
```

A parser selected by `let!` returns `RefinementError.ParseFailed` on failure. A selected refinement constructor returns
its structured `RefinementError` directly. Explicit `Result<_, ParseError>` and `Result<_, RefinementError>` values
still bind normally.

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
        let! (parsedId: int) = rawId
        let! (positiveId: PositiveInt) = parsedId
        let! (email: NonBlankString) = rawEmail
        
        return {
            Id = UserId positiveId
            Email = Email email
        }
    }
```

The annotations are required here because a `string` can target several parsed and refined types, while an `int` can
target several integer refinements. F# must resolve the `Bind` overload before the final `return` establishes the
block's result type.

## Automatic parsing

Bind raw text to any concrete primitive parser target:

| Left-hand type | Selected parser |
| --- | --- |
| `int` | `Parse.int` |
| `int64` | `Parse.long` |
| `decimal` | `Parse.decimal` |
| `float` | `Parse.float` |
| `bool` | `Parse.bool` |
| `Guid` | `Parse.guid` |
| `DateTime` | `Parse.dateTime` |
| `DateTimeOffset` | `Parse.dateTimeOffset` |
| `DateOnly` | `Parse.dateOnly` on .NET 8+ |
| `TimeOnly` | `Parse.timeOnly` on .NET 8+ |

`Parse.enum`, optional parsers, and parsers with caller-supplied defaults need additional information that a target
type alone cannot provide. Call those functions explicitly; their `Result<_, ParseError>` values bind directly.

## Automatic refinement

The refined type on the left selects its matching `Refine` constructor:

| Raw right side | Left-hand type | Selected constructor |
| --- | --- | --- |
| `string` | `NonBlankString` | `Refine.nonBlankString` |
| `string` | `TrimmedString` | `Refine.trimmedString` |
| `string` | `Slug` | `Refine.slug` |
| `int` | `PositiveInt` | `Refine.positiveInt` |
| `int` | `NonNegativeInt` | `Refine.nonNegativeInt` |
| `int` | `NonZeroInt` | `Refine.nonZeroInt` |
| `int` | `NegativeInt` | `Refine.negativeInt` |
| `int` | `NonPositiveInt` | `Refine.nonPositiveInt` |
| `seq<'value>` | `NonEmptyList<'value>` | `Refine.nonEmptyList` |
| `seq<'value>` | `NonEmptyArray<'value>` | `Refine.nonEmptyArray` |
| `seq<'value>` | `DistinctList<'value>` | `Refine.distinctList` |

Refinements that need configuration take that configuration beside the raw input:

```fsharp
refine {
    let! (name: BoundedString) = (rawName, 3, 80)
    let! (items: BoundedList<Item>) = (rawItems, 1, 20)
    let! (codes: BoundedArray<string>) = (rawCodes, 1, 10)
    let! (window: DateTimeOffsetRange) = (startsAt, endsAt)
    let! (dates: DateOnlyRange) = (firstDate, lastDate)
    return name, items, codes, window, dates
}
```

These select `Refine.boundedString`, `Refine.boundedList`, `Refine.boundedArray`,
`Refine.dateTimeOffsetRange`, and `Refine.dateOnlyRange`. The list form accepts a list, the array form accepts an
array, and `DateOnlyRange` is available on .NET 8 and later.

Explicit constructors remain useful when a function computes configuration dynamically or when local code reads more
clearly with the operation named:

```fsharp
refine {
    let! name = Refine.boundedString minimum maximum rawName
    return name
}
```

## Define your own destination type

The builder uses the same type selection as `Refine.from`. Define a static `RefineFrom` member on your destination
type:

```fsharp
type CustomerId =
    private
    | CustomerId of PositiveInt

    static member RefineFrom(raw: string, _: CustomerId) : Result<CustomerId, RefinementError> =
        refine {
            let! (parsed: int) = raw
            let! (positive: PositiveInt) = parsed
            return CustomerId positive
        }
```

The type then binds like a built-in refinement:

```fsharp
refine {
    let! (customerId: CustomerId) = rawCustomerId
    let! (quantity: PositiveInt) = rawQuantity
    return customerId, quantity
}
```

Define one `RefineFrom` member for each source and destination pair. Two interpretations with the same pair have no
type-level distinction, so they require explicitly named functions.

## When to use `refine {}`

- **Parsing Strings**: When reading values from query strings, environment variables, or CLI inputs.
- **Fail-Fast Boundary Validation**: When constructing values where the type system itself guarantees the invariant (e.g. you cannot have a user record with an empty email).

If you need to collect multiple independent errors across fields without failing fast, use [`validate {}`]({{< relref "/error-handling/diagnostics/validate-builder.md" >}}) instead.
