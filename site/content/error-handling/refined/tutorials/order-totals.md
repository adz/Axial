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
      Quantity: PositiveInt
      UnitPrice: PositiveDecimal }

type Order =
    { Reference: NonBlankString
      Lines: NonEmptyList<OrderLine>
      Discount: UnitInterval
      Delivery: Interval<DateTimeOffset> }
```

Read the field types as a specification. An order has at least one line. A line's quantity
is at least one and its price is above zero. The discount is a proportion, so it cannot be
140% or `NaN`. The delivery window's start is not after its end. None of those facts needs
restating later, because none of them can be false.

## Admit the input

```fsharp
let orderLine rawSku rawQuantity rawPrice =
    result {
        let! sku = Refine.nonBlankString rawSku
        let! quantity = Refine.positiveInt rawQuantity
        let! price = Refine.positiveDecimal rawPrice
        return { Sku = sku; Quantity = quantity; UnitPrice = price }
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

### Line totals

Quantity and price are both positive, so widening is total and needs no revalidation:

```fsharp
let lineTotal (line: OrderLine) =
    PositiveDecimal.multiply (PositiveInt.toDecimal line.Quantity) line.UnitPrice
```

`toDecimal` cannot fail — a positive `int` is a positive `decimal`. `multiply` returns a
`Result` because `decimal` can still overflow, which is the one thing the type genuinely
cannot promise.

### Subtotal

```fsharp
let subtotal (order: Order) =
    order.Lines
    |> NonEmptyList.traverseResult lineTotal
    |> Result.bind PositiveDecimal.sum
```

`traverseResult` maps a fallible function across the lines and accumulates *every* failure
rather than stopping at the first. It returns `NonEmptyList<PositiveDecimal>`, so `sum`
needs no seed and no empty case — and the subtotal of positive amounts is itself positive.

### Discount

```fsharp
let payable (order: Order) =
    subtotal order
    |> Result.map (fun total ->
        let multiplier = UnitInterval.complement order.Discount
        total.Value * decimal (UnitInterval.value multiplier))
```

`complement` is total and closed, so `multiplier` is guaranteed to be in `[0, 1]`. That is
what makes the result safe without a check: the payable amount cannot exceed the subtotal
and cannot go negative, because there is no discount value that would allow it.

The conversion to `decimal` is deliberate rather than hidden. `UnitInterval` is a double,
money is a `decimal`, and mixing the two is a rounding decision that belongs in your code
rather than behind an implicit widening.

This is also where the invariant stops. Money leaves as a `decimal` because that is what
the next system wants; re-admit it with `NonNegativeDecimal.create` if it must stay refined.

### Statistics

```fsharp
let largestLine (order: Order) =
    order.Lines |> NonEmptyList.maxBy (fun line -> line.Quantity.Value)

let lineCount (order: Order) =
    NonEmptyList.length order.Lines

let averageUnitPrice (order: Order) =
    let prices = order.Lines |> NonEmptyList.map (fun line -> line.UnitPrice.Value)
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
    |> Result.map DistinctList.toMap
```

`Map.ofList` on an ordinary list keeps only the last of each duplicate key and reports
nothing. `DistinctList.toMap` cannot lose an entry, because the type it consumes cannot
contain one to lose.

## What the invariants removed

| Fact carried by a type | Branch it removed |
|---|---|
| `NonEmptyList` has a first item | no `tryHead`, no option from `max`/`reduce` |
| `NonEmptyList` has a positive length | no divide-by-zero on an average |
| `PositiveInt`, `PositiveDecimal` | no "is this above zero" guard before widening |
| `UnitInterval` is in `[0, 1]` | no clamping the multiplier before applying it |
| `Interval` has `Lower <= Upper` | no "did they send these backwards" check |
| `DistinctList` has no duplicates | no silent key collision building a map |

None of these is a claim about construction. Each is a claim about every line of code
downstream.

## Next

- [Built-in Refined Values](../../catalog/) — what each type is closed under.
- [Customer Id](../customer-id/) — define a refined type of your own, and give it a schema.
- [Compose Parse and Refinement](../../composition/) — mapping failures to application errors.
