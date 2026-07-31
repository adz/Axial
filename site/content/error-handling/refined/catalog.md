---
weight: 20
title: Built-in Refined Values
description: The invariant-carrying types the package supplies, and the operations that justify each one.
type: docs
---


A refined type earns its place by what it lets you *stop writing*. Each type below makes
some partial operation total, guarantees a property later operations rely on, or removes a
branch from every consumer. A wrapper that only validates at construction is a constraint,
not a type — see [When not to make a type](#when-not-to-make-a-type).

```fsharp
open Axial.Check
open Axial.Refined
```

## What each type buys you

| Type | Closed under | Made total |
|---|---|---|
| `NonEmptyList<'T>`, `NonEmptyArray<'T>` | `map`, `append`, `rev`, `sort`, `distinct` | `head`, `last`, `reduce`, `min`, `max` |
| `Interval<'T>` | `between`, `span`, `clamp`, `mapMonotonic` | `contains`, `clamp` |
| `Bounded<'T>` | `clamp`, `map` (re-clamps) | `clamp` |
| `PositiveInt`, `NonNegativeInt`, … | `min`, `max`, saturating `+`/`*` | — |
| `NonZeroInt`, … | `negate` | removes divide-by-zero |
| `UnitInterval` | `*`, `complement`, `lerp`, `min`, `max` | `clamp` |
| `FiniteFloat` | `negate`, `abs` | `compare`, sorting, `Map` keys |
| `DistinctList<'T>` | `add`, `remove`, `union`, `intersect` | `toMap`, `toSet` |
| `NonBlankString` | `append`, `trim`, `toUpper`, `toLower` | `split` |

## Collections

`NonEmptyList` carries its non-emptiness in the representation, so the case is public and
you can pattern match on it:

```fsharp
let lines = NonEmpty(firstLine, remainingLines)   // total, no Result

let (NonEmpty(first, rest)) = lines               // total
let total = NonEmptyList.reduce (+) lines         // total, needs no seed
let largest = NonEmptyList.max lines              // total, no option
```

`NonEmptyList.create` admits an ordinary sequence and returns
`Result<NonEmptyList<'T>, CheckFailure list>`; `NonEmptyList.ofList` returns an option.

Filtering can remove every item, so `filter` returns an ordinary list and `tryFilter`
returns an option. `traverseResult` applies a fallible mapping across the list and
accumulates every failure rather than stopping at the first.

`NonEmptyArray` stays smart-constructed rather than structural. A head-and-tail
representation would forfeit contiguous storage and indexed access, which are the reasons
to choose an array; the total `head`/`last`/`reduce`/`max` still apply.

`DistinctList` exists for one operation: converting to a map or set without silently
losing entries. `Map.ofList` on a plain list quietly keeps only the last of each duplicate
key — `DistinctList.toMap` cannot.

## Intervals and bounds

One generic `Interval<'T>` covers any ordered value. It is always inhabited, so emptiness
is reported as an option rather than by a second type:

```fsharp
let window  = Interval.between start finish     // total: orders its arguments
let overlap = Interval.intersect window other   // Interval option — honest about emptiness
let clamped = Interval.clamp candidate window   // total
```

The ends are `Lower` and `Upper`: they name the two bounds' roles, not a traversal. An
interval has no direction, so `between 5 1` equals `between 1 5`.

That is why there are two constructors. `between` accepts either order and repairs it;
`create` asserts the pair is already ordered and fails when it is not. Reach for `between`
in code, and `create` at a boundary, where an inverted pair is a caller error worth
reporting rather than silently swapping.

`union` returns `None` when the two intervals do not overlap, because joining them would
invent a gap; `span` closes the gap deliberately.

For instants, `DateRange` abbreviates `Interval<DateTimeOffset>` and
`RefinedSchemas.dateRange` uses `start`/`end` on the wire. That is a schema-level naming
choice, not a second type — every `Interval` operation applies unchanged.

`Bounded<'T>` pairs a value with the interval it must stay inside. Bounds are carried at
run time, so `Bounded.clamp` is total and `Bounded.map` re-clamps — a mapping cannot break
the invariant.

## Numeric

**These types are not closed under arithmetic, and the API reflects that.** F# integer
arithmetic is unchecked: `Int32.MaxValue + 1` is negative. An addition returning
`PositiveInt` would hand back a value violating its own invariant, so each module offers
two forms:

```fsharp
PositiveInt.add a b            // Result — reports overflow
PositiveInt.saturatingAdd a b  // total — clamps at maxValue
PositiveInt.min a b            // total, always
```

Available: `PositiveInt`, `NonNegativeInt`, `NonZeroInt`, and their `Int64` and `Decimal`
counterparts. Widening is total and needs no revalidation —
`PositiveInt.toNonNegative`, `PositiveInt.toNonZero`.

`NonZero` is justified by branch removal: `DivideByZeroException` becomes unreachable, so
consumers stop guarding for it. Overflow is still possible (`Int32.MinValue / -1`), so
`divide` returns a `Result` and `saturatingDivide` is total.

## Floating point

`FiniteFloat` excludes `NaN` and the infinities. Its value is **lawful ordering**, not safe
arithmetic: `NaN` compares false against every value including itself, which silently
corrupts sorting and makes `float` unusable as a `Map` key.

```fsharp
List.sort finiteValues        // total and order-independent
Map [ finiteKey, value ]      // no silent collisions
FiniteFloat.negate value      // closed
FiniteFloat.add a b           // Result — two finite doubles can reach infinity
```

`UnitInterval` holds a proportion in `[0, 1]`. It is the only type here closed under
multiplication, which is the reason to reach for it:

```fsharp
UnitInterval.multiply a b         // total and closed
UnitInterval.complement a         // total
UnitInterval.lerp low high a      // total, always lands between the endpoints
UnitInterval.saturatingAdd a b    // not closed under +, so this clamps
```

`complement` is an involution only up to floating-point rounding — exact for dyadic values,
approximate otherwise.

## Text

`NonBlankString` preserves accepted text exactly, and its operations preserve inhabitation:

```fsharp
NonBlankString.append first second   // total
NonBlankString.trim value            // total — trimming inhabited text leaves it inhabited
NonBlankString.split "," value       // NonEmptyList<NonBlankString>, never empty
```

## When not to make a type

Trimmed text, slugs, email addresses, and length bounds carry no invariant that any later
operation uses. Concatenating two trimmed strings is not trimmed; you unwrap at first use.
Express them as constraints on a primitive instead — the metadata reaching interpreters is
identical:

```fsharp
field "displayName" _.DisplayName {
    withSchema (Schema.text |> Schema.constrain Constraint.trimmed)
}

field "slug" _.Slug {
    withSchema (Schema.text |> Schema.constrainAll [ Constraint.present; Constraint.pattern slugPattern ])
}
```

If you do want a nominal type in your own domain, the machinery is still here — see
[Define Refined Types](../domain-values/).

## Schema resolution

Every type above has a canonical wire schema, so a bare field resolves it with no
`withSchema`. The 64-bit and floating-point types sit on the `Schema.int64` and
`Schema.float` primitives rather than being mapped onto `decimal`, which would change
their meaning. Note that JSON has no literal for `NaN` or the infinities: a schema that
must reject them should use `FiniteFloat`, whose `finite` constraint is inspectable
metadata like any other.

Continue with [Compose Parse and Refinement](../composition/) and
[Define Refined Types](../domain-values/).
