---
weight: 10
title: Order Totals Tutorial
description: Model an order with refined types, then watch the invariants remove branches from the code that uses it.
type: docs
---


This tutorial builds an order from untrusted input and then calculates over it. The point
is what happens *after* construction: every invariant admitted at the boundary removes a
branch, an option, or a guard from the code downstream.

```fsharp
open System
open Axial.Check
open Axial.Refined
```

## Model the domain

```fsharp
type OrderLine =
    { Sku: NonBlankString
      Quantity: int
      UnitPrice: decimal }

type Order =
    { Reference: NonBlankString
      Lines: NonEmptyList<OrderLine>
      Discount: UnitInterval
      Delivery: Interval<DateTimeOffset> }
```

Read the field types as a specification. An order has at least one line. The discount is a
proportion, so it cannot be 140% or `NaN`. The delivery window's start is not after its
end. None of those facts needs restating later, because none of them can be false.

Quantity and price are ordinary numbers, checked on the way in. They are not refined types
because F# cannot carry "greater than zero" through arithmetic — see
[why there are no refined numbers](../../catalog/#why-there-are-no-refined-numbers).

## Admit the input

```fsharp
let orderLine rawSku rawQuantity rawPrice =
    result {
        let! sku = Refine.nonBlankString rawSku
        let! _ = Check.greaterThan 0 rawQuantity
        let! _ = Check.greaterThan 0m rawPrice
        return { Sku = sku; Quantity = rawQuantity; UnitPrice = rawPrice }
    }
```

Invalid values are rejected here and nowhere else:

```fsharp
orderLine "SKU-1" 0 9.99m      // Error [ OutOfRange (GreaterThan "0", Some "0") ]
orderLine "   " 1 9.99m        // Error [ Blank ]
Refine.nonEmptyList ([]: OrderLine list)
                               // Error [ InvalidLength (MinimumLength 1, Some 0) ]
UnitInterval.create 1.4        // Error [ OutOfRange (Between ("0", "1"), Some "1.4") ]
UnitInterval.create Double.NaN // Error — NaN is outside every interval
```

Two of the four fields have a **total** constructor, which is the one to prefer when the
input has an obvious correct reading:

```fsharp
let window = Interval.between requestedFrom requestedTo  // cannot fail: orders the pair
let discount = UnitInterval.clamp rawDiscount            // cannot fail: clamps into [0, 1]
```

`Interval.between` accepts the two instants in either order. Use `Interval.create` instead
when an inverted pair means the caller made a mistake you would rather report than repair.

## Calculate, without re-checking anything

### Line and order totals

The numbers are plain, so the arithmetic is plain:

```fsharp
let lineTotal (line: OrderLine) = decimal line.Quantity * line.UnitPrice

let subtotal (order: Order) =
    order.Lines |> NonEmptyList.map lineTotal |> NonEmptyList.reduce (+)
```

`reduce` needs no seed and no empty case — that is the invariant paying, and it pays
without putting a `Result` between every operation.

### Discount

```fsharp
let payable (order: Order) =
    let multiplier = UnitInterval.complement order.Discount
    subtotal order * decimal (UnitInterval.value multiplier)
```

`complement` is total and closed, so `multiplier` is guaranteed to be in `[0, 1]`. That is
what makes the result safe without a check: the payable amount cannot exceed the subtotal
and cannot go negative, because there is no discount value that would allow it.

The conversion to `decimal` is deliberate rather than hidden. `UnitInterval` is a double,
money is a `decimal`, and mixing the two is a rounding decision that belongs in your code.

### Statistics

```fsharp
let largestLine (order: Order) =
    order.Lines |> NonEmptyList.maxBy (fun line -> line.Quantity)

let lineCount (order: Order) =
    NonEmptyList.length order.Lines

let averageUnitPrice (order: Order) =
    let prices = order.Lines |> NonEmptyList.map (fun line -> line.UnitPrice)
    NonEmptyList.reduce (+) prices / decimal (NonEmptyList.length prices)
```

`maxBy` returns an `OrderLine`, not an option. `reduce` needs no seed. Dividing by
`length` cannot divide by zero. Each of those is a branch the plain-list version would
have had to write:

```fsharp
// what the same three functions cost over an ordinary list
let largestLine lines = lines |> List.sortByDescending (fun l -> l.Quantity) |> List.tryHead
let averageUnitPrice lines =
    if List.isEmpty lines then None
    else Some (List.sumBy _.UnitPrice lines / decimal (List.length lines))
```

### Delivery window

```fsharp
let isDeliverable (order: Order) (candidate: DateTimeOffset) =
    Interval.contains candidate order.Delivery

let overlapWith (order: Order) (other: Interval<DateTimeOffset>) =
    Interval.intersect order.Delivery other   // Interval option — None when disjoint
```

`intersect` returns an option because two windows may not overlap. That is the honest
shape: an empty interval is not representable, so emptiness is reported rather than
smuggled into a value whose `Lower` is somehow above its `Upper`.

## Catch a duplicate the type system can see

Distinctness is a relationship between values, so it needs a checked constructor — but the
resulting type then converts to a map without silently dropping entries:

```fsharp
let skus (order: Order) =
    order.Lines
    |> NonEmptyList.map (fun line -> NonBlankString.value line.Sku)
    |> NonEmptyList.toList
    |> DistinctList.create      // Error [ Duplicate ] when the same SKU appears twice

let lineBySku (order: Order) =
    order.Lines
    |> NonEmptyList.toList
    |> List.map (fun line -> NonBlankString.value line.Sku, line)
    |> DistinctList.create
    |> Result.bind DistinctList.toMap
```

`Map.ofList` on an ordinary list keeps only the last of each duplicate key and reports
nothing. `DistinctList.toMap` returns a `Result` instead: distinctness holds over whole
pairs rather than over keys, so it checks the keys and tells you about a collision rather
than losing an entry.

## What the invariants removed

| Fact carried by a type | Branch it removed |
|---|---|
| `NonEmptyList` has a first item | no `tryHead`, no option from `max`/`reduce` |
| `NonEmptyList` has a positive length | no divide-by-zero on an average |
| `UnitInterval` is in `[0, 1]` | no clamping the multiplier before applying it |
| `Interval` has `Lower <= Upper` | no "did they send these backwards" check |
| `DistinctList` has no duplicates | no silent collapse building a set; a reported failure building a map |

None of these is a claim about construction. Each is a claim about every line of code
downstream.

## Next

- [Built-in Refined Values](../../catalog/) — what each type is closed under.
- [Customer Id](../customer-id/) — define a refined type of your own, and give it a schema.
- [Compose Parse and Refinement](../../composition/) — mapping failures to application errors.
