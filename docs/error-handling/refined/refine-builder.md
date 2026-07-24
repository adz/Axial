---
weight: 30
title: Refine Computation Expression
description: Use destination types to sequence parsing and refinement with refine { }.
---

# Refine Computation Expression

The direct form names every operation:

```fsharp
let quantity (raw: string) : Result<PositiveInt, RefinementError> =
    refine {
        let! parsed = Parse.int raw
        return! Refine.positiveInt parsed
    }
```

`let!` takes the value from a successful `Parse` or `Refine` result. `return!` returns an existing refinement result.
`refine {}` converts `ParseError` into `RefinementError` and stops at the first failure.

The builder also supports type-directed operations:

```fsharp
let quantity (raw: string) : Result<PositiveInt, RefinementError> =
    refine {
        let! (parsed: int) = raw
        let! (positive: PositiveInt) = parsed
        return positive
    }
```

The type on the left of `let!` is part of the operation. `raw` is a `string`, and `parsed` is an `int`, so Axial
resolves the built-in `Refinement<string, int>`. The next line resolves `Refinement<int, PositiveInt>`.

## Builder operations

The builder accepts three forms:

```fsharp
refine {
    // Raw value: resolve Refinement<string, int>, then run it.
    let! (parsed: int) = rawId

    // Parse result: map ParseError into RefinementError.
    let! count = Parse.int rawCount

    // Refinement result: bind it without another conversion.
    do! (checkAllowed parsed: Result<unit, RefinementError>)

    // Existing refinement result: use it as the result of the block.
    return! createAccount parsed count
}
```

`return` produces `Ok value`. `do!` requires a successful `unit` result before continuing.

The same block with the relevant types made explicit is:

```fsharp
refine {
    let! (parsed: int) = (rawId: string)
    let! (count: int) = (Parse.int rawCount: Result<int, ParseError>)
    do! (checkAllowed parsed: Result<unit, RefinementError>)
    return! (createAccount parsed count: Result<Account, RefinementError>)
}
```

## Why the annotation is sometimes required

One `string` can become an `int`, `Guid`, `DateTimeOffset`, `NonBlankString`, or an application type. F# must know the
destination while it resolves `let!`:

```fsharp
let! (id: int) = rawId
let! (email: ContactEmail) = rawEmail
```

The final return type arrives too late to disambiguate those bind operations. This is the same reason
`Refine.from rawId` normally has a result annotation:

```fsharp
let id : Result<int, RefinementError> = Refine.from rawId
```

## Built-in parsing

Raw text can target these primitive types:

| Destination | Parser used by its refinement |
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

The descriptor wraps `ParseError` as `RefinementError.ParseFailed`. `Parse.enum`, optional parsers, and parsers with a
supplied default need arguments that a destination type cannot contain, so call those functions directly.

## Built-in refined values

The destination also selects built-in structural refinements:

| Raw value | Destination |
| --- | --- |
| `string` | `NonBlankString`, `TrimmedString`, or `Slug` |
| `int` | `PositiveInt`, `NonNegativeInt`, `NonZeroInt`, `NegativeInt`, or `NonPositiveInt` |
| `seq<'value>` or a supported concrete collection | `NonEmptyList<'value>`, `NonEmptyArray<'value>`, or `DistinctList<'value>` |

Configured refinements include their configuration in the raw input type:

```fsharp
refine {
    let! (name: BoundedString) = (rawName, 3, 80)
    let! (items: BoundedList<Item>) = (rawItems, 1, 20)
    let! (window: DateTimeOffsetRange) = (startsAt, endsAt)
    return name, items, window
}
```

For example, the first line resolves
`Refinement<string * int * int, BoundedString>`.

## Application types use the same protocol

An application type contributes a `Refinement` descriptor:

```fsharp
type CustomerId =
    private
    | CustomerId of PositiveInt

module CustomerId =
    let create raw : Result<CustomerId, RefinementError> =
        refine {
            let! (parsed: int) = raw
            let! (positive: PositiveInt) = parsed
            return CustomerId positive
        }

    let value (CustomerId value) =
        string value.Value

    let refinement =
        Refinement.define create value

type CustomerId with
    static member Refinement(_: string, _: CustomerId) =
        CustomerId.refinement
```

It then works in both entry points:

```fsharp
let one : Result<CustomerId, RefinementError> =
    Refine.from rawCustomerId

let pair =
    refine {
        let! (customerId: CustomerId) = rawCustomerId
        let! (quantity: PositiveInt) = rawQuantity
        return customerId, quantity
    }
```

Continue with [Define Refined Types](../domain-values/) to add type-directed construction to an application type.
