---
weight: 40
title: Refined
type: docs
description: Parse text and construct values whose types record successful checks.
---

# Refined

Boundary values usually begin as strings and ordinary F# values. `Axial.Refined` provides two direct sets of
functions for making those values safer:

- `Parse` converts serialized text to an F# type.
- `Refine` checks a value and returns a type that records the check.

Install the package and open its namespace:

```sh
dotnet add package Axial.Refined
```

```fsharp
open Axial.Refined
```

## Parse text

`Parse.int` converts text to an `int`:

```fsharp
let count : Result<int, ParseError> =
    Parse.int "42"
// Ok 42

let invalidCount : Result<int, ParseError> =
    Parse.int "many"
// Error (InvalidFormat ("int", "many"))
```

The function returns `Result` because any string can be passed to it. `Parse.guid`, `Parse.decimal`,
`Parse.dateTimeOffset`, and the other parsing functions follow the same form.

## Refine a value

`Refine.nonBlankString` checks a string and returns `NonBlankString`:

```fsharp
let name : Result<NonBlankString, RefinementError> =
    Refine.nonBlankString "Ada"
// Ok ...

let missingName : Result<NonBlankString, RefinementError> =
    Refine.nonBlankString "   "
// Error ...
```

The result contains a `NonBlankString`, not another `string`. Code that accepts `NonBlankString` therefore cannot
receive an unchecked string.

Other constructors work the same way:

```fsharp
let quantity = Refine.positiveInt 3
// Result<PositiveInt, RefinementError>

let items = Refine.nonEmptyList [ "first"; "second" ]
// Result<NonEmptyList<string>, RefinementError>

let slug = Refine.slug "release-notes"
// Result<Slug, RefinementError>
```

Read the underlying value through the matching type module:

```fsharp
let printName (name: NonBlankString) =
    printfn "%s" (NonBlankString.value name)
```

## Parse, then refine

Parsing and refinement are separate operations. Parsing answers whether `"12"` is an integer. Refinement answers
whether that integer is positive.

```fsharp
let quantity (raw: string) : Result<PositiveInt, RefinementError> =
    refine {
        let! parsed = Parse.int raw
        return! Refine.positiveInt parsed
    }
```

`refine { }` converts `ParseError` to `RefinementError` and stops at the first failure. Named `Parse` and `Refine`
functions remain visible, so this form is useful before learning type-directed refinement.

After the direct functions are familiar, destination types can remove repeated function names:

```fsharp
let quantity (raw: string) : Result<PositiveInt, RefinementError> =
    refine {
        let! (parsed: int) = raw
        let! (positive: PositiveInt) = parsed
        return positive
    }
```

The annotation on each `let!` tells the builder which built-in operation to run. The first line uses the same
conversion as `Parse.int`; the second uses the same check as `Refine.positiveInt`.

For one type-directed operation, use `Refine.from`:

```fsharp
let parsed : Result<int, RefinementError> =
    Refine.from "42"

let positive : Result<PositiveInt, RefinementError> =
    Refine.from 42
```

## Read next

1. [Parse](./parse/) lists the direct parsing forms and their errors.
2. [Built-in Refined Values](./catalog/) covers the supplied refined types and constructors.
3. [Refine Computation Expression](./refine-builder/) explains explicit and type-directed `let!`.
4. [Define Refined Types](./domain-values/) builds a private wrapper and smart constructor, then makes it available to
   `Refine.from`.
5. [Schema Integration](./schema/) shows how a refinement is used in an `Axial.Schema` declaration.

The [Refined API reference]({{< relref "/error-handling/reference/refined/" >}}) lists every public type and function.
