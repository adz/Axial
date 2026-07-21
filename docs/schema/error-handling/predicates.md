---
weight: 5
title: Predicates
description: Plain bool facts for local branching, distinct from Check.
---

# Predicates

`Predicate` and `PredicateExtensions` give you the same structural facts as `Check`, but as plain `bool`.

```fsharp
open Axial

if name.IsBlank then
    failwith "name is required"

let isValidAge = Predicate.Number.between 13 120 age
```

## Fluent Extension Members

`PredicateExtensions` is `AutoOpen`, so opening `Axial.ErrorHandling` (or any namespace that opens it) adds `bool`
members directly onto the types they describe — no module qualification needed at the call site.

```fsharp
"".IsEmpty          // true
"  ".IsBlank        // true
"a@b.com".IsEmail   // true
[1; 2; 3].HasItems  // true
(Some 1).IsPresent  // true
```

| Type | Members |
| --- | --- |
| `string` | `IsEmpty`, `IsNotEmpty`, `IsBlank`, `IsNotBlank`, `IsPresent`, `IsAbsent`, `IsEmail`, `IsNumeric`, `IsAlphaNumeric`, `HasMinLength`, `HasMaxLength`, `HasLength`, `HasLengthBetween`, `MatchesPattern` |
| `'value option` | `IsPresent`, `IsAbsent` |
| `'value voption` | `IsPresent`, `IsAbsent` |
| `Nullable<'value>` | `IsPresent`, `IsAbsent` |
| `Result<'value, 'error>` | `IsOk`, `IsError` |
| `IEnumerable<'value>` (lists, arrays, seqs) | `HasNoItems`, `HasItems`, `IsPresent`, `IsAbsent`, `HasCount`, `HasMinCount`, `HasMaxCount`, `HasCountBetween`, `HasSingleItem`, `HasAtMostOneItem`, `HasMoreThanOneItem`, `HasItem`, `HasDuplicates`, `IsDistinct` |

`IsPresent`/`IsAbsent` mean "non-blank" for strings, not merely "non-null" — an all-whitespace string is absent.
`IsEmpty`/`IsNotEmpty` on strings are the stricter, length-based check instead.

## The `Predicate` Module

Values that don't have an obvious type to hang an extension member off — comparisons against a caller-supplied
bound, or null checks on an unconstrained reference type — live as plain functions in the `Predicate` module
instead:

```fsharp
Predicate.Reference.isNull value
Predicate.Reference.notNull value

Predicate.Number.greaterThan 0 value
Predicate.Number.atLeast 18 age
Predicate.Number.between 1 10 value
Predicate.Number.positive value
Predicate.Number.nonNegative value
Predicate.Number.negative value
Predicate.Number.nonPositive value
```

`Predicate.present`, `Predicate.empty`, and `Predicate.notEmpty` are the `bool`-returning counterparts to
`Check.present`/`Check.empty`/`Check.notEmpty` — the same type-directed dispatch over `string`, `option`, `voption`,
`Nullable<'value>`, and sequence-shaped values, resolved at compile time via SRTP, just returning `bool` instead of
`Result`:

```fsharp
Predicate.present "Ada"         // true
Predicate.present (Some 1)      // true
Predicate.empty (None: int option) // true
```

## Predicate Versus Check

Both describe the same facts. The difference is what the caller does next:

- Use `Predicate`/`PredicateExtensions` when you're branching locally and never need to carry the failure anywhere —
  an `if` guard, an early return, a condition inside another expression.
- Use [`Check`](./checks/) when the outcome needs to become a `Result` — because it feeds a domain error, gets
  piped through `Result` helpers, or needs to compose with `Check.all`/`Check.any`.

```fsharp
// Predicate: the bool is consumed immediately, nothing downstream needs the failure.
let describeName name =
    if name.IsBlank then "unnamed" else name

// Check: the failure needs to become a typed Result for a caller.
let validateName name : Result<string, NameError> =
    name |> Check.present |> Result.orError NameMissing
```

If you start with a `Predicate` and find yourself converting its `bool` into an `Error` by hand
(`if not ok then Error ... else Ok value`), that's the sign to use `Check` instead — `Result.requireTrue` and
`Result.okIf` exist for the cases where a `Check` genuinely doesn't fit; see [Checks](./checks/).
