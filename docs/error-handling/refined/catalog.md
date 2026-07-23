---
weight: 20
title: Refined Catalog
description: Built-in refined values and helper modules in Axial.Refined.
---

# Refined Catalog

This page lists the built-in refined values in `Axial.Refined`, together with the input and output type of each helper.

`Axial.Refined` is not only a type catalog: it also owns primitive parsing, smart constructors, the `refine {}` builder, and parser-choice helpers.

For your own domain values, use the single authoring pattern in [Domain Values](../domain-values/): private
constructor, smart constructor, optional standalone helper, and `Schema.refine` schema when the type appears in a model.

When the same scalar catalog value is used as a schema field, use the schema integration catalog in
`Axial.Schema`:

```fsharp
open Axial.Schema

let productSchema =
    schema<Product> {
        field "name" _.Name
        field "slug" _.Slug
        field "quantity" _.Quantity
        construct (fun name slug quantity ->
            { Name = name; Slug = slug; Quantity = quantity })
    }
```

The canonical schemas live outside `Axial.Refined` so the refined-value package stays usable without any schema dependency.
Collection schema helpers such as `RefinedSchemas.nonEmptyList RefinedSchemas.slug` and
`RefinedSchemas.distinctList Schema.text` take an item value schema, so item parsing and item diagnostics stay explicit.
Temporal range helpers such as `RefinedSchemas.dateTimeOffsetRange` are complete model schemas with `start` and `end`
fields. `RefinedSchemas.dateOnlyRange` is available when targeting frameworks that support `DateOnly`.

## Module Layout

Open `Axial.Refined` for common type names and use submodules for discovery:

```fsharp
open Axial.Refined

let name = Refine.nonBlankString "Ada"
let sameName = Text.nonBlankString "Ada"
let count = Numeric.positiveInt 3
```

Use `Refine` as the convenience facade. Use `Text`, `Numeric`, `Collection`, `Temporal`, `Character`, and `Choice` when grouping matters in larger code.

## Numeric

Use numeric refinements when a raw `int` would permit invalid domain values.

```fsharp
let quantity : Result<PositiveInt, RefinementError> =
    Refine.positiveInt 3

let optionalOffset : Result<NonNegativeInt, RefinementError> =
    Refine.nonNegativeInt 0

let databaseId : Result<NonZeroInt, RefinementError> =
    Refine.nonZeroInt 42
```

Available integer wrappers:

- `PositiveInt`: greater than zero.
- `NonNegativeInt`: greater than or equal to zero.
- `NonZeroInt`: not zero.
- `NegativeInt`: less than zero.
- `NonPositiveInt`: less than or equal to zero.

Float refinements and `Percentage` are intentionally absent. They need explicit decisions about `NaN`, infinities, negative zero, and percentage scale.

## Text

Use text refinements to distinguish raw strings from strings that have already passed boundary rules.

```fsharp
let displayName =
    Refine.nonBlankString "Ada Lovelace"

let commandName =
    Refine.trimmedString "deploy"

let slug =
    Refine.slug "release-notes"

let shortCode =
    Refine.boundedString 2 8 "AX42"
```

Important semantics:

- `NonBlankString` rejects null, empty, and whitespace-only strings. It preserves the accepted value exactly.
- `TrimmedString` proves the value already has no leading or trailing whitespace. It does not trim during construction.
- `BoundedString` stores the min/max bounds used for construction.
- `Slug` is ASCII-only: lowercase letters, digits, and hyphens, with no leading, trailing, or repeated hyphen.

Regex-backed values, email addresses, URLs, telephone numbers, postal codes, and sanitized text are intentionally absent. Regex adds dependency and timeout concerns; sanitizing text transforms input rather than simply refining it.

## Collections

Use collection refinements when the collection shape matters to later logic.

```fsharp
let ids =
    Refine.nonEmptyList [ 1; 2; 3 ]

let names =
    Refine.nonEmptyArray [ "Ada"; "Grace" ]

let tags =
    Refine.distinctList [ "fsharp"; "typed-errors" ]

let batch =
    Refine.boundedList 1 100 [ 1; 2; 3 ]
```

Available collection wrappers:

- `NonEmptyList<'T>`: exposes `Head`, `Tail`, `ToList()`, and `seq<'T>`.
- `NonEmptyArray<'T>`: exposes `Head`, `Tail`, `ToArray()`, and `seq<'T>`.
- `DistinctList<'T>`: rejects duplicates and preserves first-seen order.
- `BoundedList<'T>`: stores list plus inclusive min/max length.
- `BoundedArray<'T>`: stores array plus inclusive min/max length.

Filtering can destroy a collection invariant. Use helpers whose result type admits that:

```fsharp
let values =
    Refine.nonEmptyList [ 1; 2; 3 ]

let evens : int list =
    values
    |> Result.map (NonEmptyList.filter (fun value -> value % 2 = 0))
    |> Result.defaultValue []

let nonEmptyEvens : Result<NonEmptyList<int>, RefinementError> =
    values
    |> Result.bind (NonEmptyList.tryFilter (fun value -> value % 2 = 0))
```

`BoundedSeq` and fixed-size arrays are intentionally absent. A .NET sequence may be lazy, single-use, infinite, or effectful, and plain F# does not make array length a normal type-level value.

## Temporal

Use temporal refinements only for stable facts.

```fsharp
let start = DateTimeOffset.Parse "2026-06-28T09:00:00Z"
let finish = start.AddDays 7.0

let range =
    Refine.dateTimeOffsetRange start finish
```

`DateTimeOffsetRange` proves `Start <= End`.

`DateOnlyRange` is available on target frameworks that support `DateOnly`.

`FutureDateTime` and `PastDateTime` are intentionally absent. A value that is future now can become past later without mutation, so clock-relative facts need an explicit clock policy.

## Character

Character helpers are predicates rather than wrappers:

```fsharp
Character.isAsciiDigit '7'
Character.isAsciiHexDigit 'f'
Character.isLowercase 'a'
Character.isUppercase 'A'
Character.isWhitespace ' '
Character.isControl '\u0001'
Character.isNumeric '9'
```

Use these helpers when building your own named wrappers such as `HexDigitChar` or `UppercaseInitial`.

## Optional serialized input

Optional parsing distinguishes absent input from malformed present input:

```fsharp
Parse.optional Parse.int None
// Ok None

Parse.optional Parse.int (Some "42")
// Ok (Some 42)

Parse.optional Parse.int (Some "bad")
// Error (InvalidFormat ("int", "bad"))
```

Use `Parse.optionalOr` when absence has a domain default. The fallback applies only to `None`; a present malformed
value remains an error:

```fsharp
maybePort |> Parse.optionalOr 80 Parse.int
```

Primitive-specific helpers provide the same behavior with discoverable names:

```fsharp
maybeCount |> Parse.intOption
maybePort |> Parse.intOrDefault 80
```

`Parse.intOption` returns `Result<int option, ParseError>`, and `Parse.intOrDefault` returns
`Result<int, ParseError>`. The corresponding Boolean, decimal, and GUID helpers follow the same rule: optionality
handles absence, never parsing failure.

## Choice

Use `Choice` when one source value may parse into several refined shapes and you want to return your own domain union.

```fsharp
type Discount =
    | Percent of PositiveInt
    | Code of Slug

let parsePercent text =
    Parse.int text
    |> Result.mapError RefinementError.ParseFailed
    |> Result.bind Refine.positiveInt

let parseDiscount raw =
    Choice.orElse
        Percent
        parsePercent
        Code
        Refine.slug
        (RefinementError.InvalidFormat("Discount", "Expected percent or code."))
        raw
```

Use `Choice.tryAny` when there are more than two alternatives:

```fsharp
let parseContact raw =
    [
        parseEmail
        parseTelephone
        parseUserName
    ]
    |> Choice.tryAny (RefinementError.InvalidFormat("Contact", "Expected email, telephone, or user name."))
    <| raw
```

Prefer this style over exposing a generic `Or<'Left, 'Right>` in domain models. The result should speak your domain language.

## Runnable Example

The generated [Runnable Examples]({{< relref "/schema/examples.md" >}}) page includes a refined catalog example that is built and executed during docs validation.
