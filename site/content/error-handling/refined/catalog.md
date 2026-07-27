---
weight: 20
title: Built-in Refined Values
description: Supplied invariant-carrying text, numeric, collection, and temporal types.
type: docs
---


Every constructor returns `Result<'refined, CheckFailure list>`.

```fsharp
open Axial.Check
open Axial.Refined
```

## Numeric

```fsharp
let quantity : Result<PositiveInt, CheckFailure list> = Refine.positiveInt 3
let offset : Result<NonNegativeInt, CheckFailure list> = Refine.nonNegativeInt 0
let databaseId : Result<NonZeroInt, CheckFailure list> = Refine.nonZeroInt 42
```

Available wrappers:

- `PositiveInt`: greater than zero.
- `NonNegativeInt`: greater than or equal to zero.
- `NonZeroInt`: not zero.
- `NegativeInt`: less than zero.
- `NonPositiveInt`: less than or equal to zero.

`PositiveInt.refinement` exposes the reusable `Refinement<int, PositiveInt>` value.

## Text

```fsharp
let displayName = Refine.nonBlankString "Ada Lovelace"
let command = Refine.trimmedString "deploy"
let slug = Refine.slug "release-notes"
let shortCode = Refine.boundedString 2 8 "AX42"
```

- `NonBlankString` preserves accepted text exactly.
- `TrimmedString` requires text that already has no surrounding whitespace.
- `BoundedString` stores the bounds used for construction.
- `Slug` accepts lowercase ASCII letters, digits, and separated hyphens.

## Collections

```fsharp
let ids = Refine.nonEmptyList [ 1; 2; 3 ]
let names = Refine.nonEmptyArray [ "Ada"; "Grace" ]
let tags = Refine.distinctList [ "fsharp"; "typed-errors" ]
let batch = Refine.boundedList 1 100 [ 1; 2; 3 ]
```

Collection wrappers use concrete canonical representations:

- `NonEmptyList<'T>` projects to `'T list`.
- `NonEmptyArray<'T>` projects to `'T array`.
- `DistinctList<'T>` projects to `'T list`.
- `BoundedList<'T>` projects to `'T list`.
- `BoundedArray<'T>` projects to `'T array`.

Filtering can remove every item, so `NonEmptyList.filter` returns an ordinary list. `NonEmptyList.tryFilter` checks the
result and returns another `NonEmptyList` when possible.

## Temporal

```fsharp
let start = System.DateTimeOffset.Parse "2026-06-28T09:00:00Z"
let finish = start.AddDays 7.0
let range = Refine.dateTimeOffsetRange start finish
```

`DateTimeOffsetRange` and `DateOnlyRange` require `Start <= End`.

## Extraction and choice

`Refine.exactlyOne` and `Refine.atMostOne` extract values after checking collection cardinality. `Choice.orElse` and
`Choice.tryAny` combine ordinary conversion functions that already share an error type.

```fsharp
type Discount = Percent of PositiveInt | Code of Slug

type DiscountError =
    | InvalidPercentText of Axial.Parse.ParseError
    | InvalidPercent of CheckFailure list
    | InvalidCode of CheckFailure list

let percent raw =
    Axial.Parse.Parse.int raw
    |> Result.mapError InvalidPercentText
    |> Result.bind (Refine.positiveInt >> Result.mapError InvalidPercent)
    |> Result.map Percent

let code raw =
    Refine.slug raw |> Result.mapError InvalidCode |> Result.map Code
```

Continue with [Compose Parse and Refinement](../composition/) and [Define Refined Types](../domain-values/).
