---
weight: 10
title: Order Reference Tutorial
description: Parse strings into refined values and your own domain type.
---

# Order Reference Tutorial

This tutorial parses two raw strings — an order id and a customer reference — into a domain record whose fields cannot
hold invalid values. Once construction succeeds, no downstream code re-checks anything.

## The Target Domain Type

Wrap refined values in your own types so signatures use the domain's language:

```fsharp
open Axial
open Axial.Refined

type OrderId = OrderId of PositiveInt
type CustomerRef = CustomerRef of Slug

type OrderReference =
    { Id: OrderId
      Customer: CustomerRef }
```

`PositiveInt` can only hold an integer greater than zero, and `Slug` only lowercase ASCII letters, digits, and single
hyphens — the types carry the proof.

## Parse, Then Refine

`Parse` turns text into primitives; `Refine` turns primitives into refined values. `refine {}` sequences both
fail-fast, unifying their errors as `RefinementError`:

```fsharp
let createOrderReference (rawId: string) (rawCustomer: string) : Result<OrderReference, RefinementError> =
    refine {
        let! parsedId = Parse.int rawId          // ParseError becomes RefinementError.ParseFailed
        let! positiveId = Refine.positiveInt parsedId
        let! customer = Refine.slug rawCustomer

        return
            { Id = OrderId positiveId
              Customer = CustomerRef customer }
    }
```

## Exercise It

```fsharp
createOrderReference "42" "acme-north"   // Ok { Id = OrderId (PositiveInt 42); Customer = ... }
createOrderReference "0" "acme-north"    // Error — zero is not positive
createOrderReference "42" "Acme North"   // Error — not a slug
createOrderReference "many" "acme-north" // Error (ParseFailed) — not an int
```

The first failure stops the pipeline. A Schema record declaration applies the same refinements independently to
boundary fields and returns their path-aware failures together.

## Read The Value Back Out

Refined types expose their underlying value for boundaries that need the primitive again:

```fsharp
let (OrderId id) = order.Id
let rawId = PositiveInt.value id
```

## Next

- [Catalog](../../catalog/) for every built-in refined type.
- [Refine CE](../../refine-builder/) for the full builder reference.
- [Refined Value Schemas]({{< relref "/schema/refined-values/" >}}) to use refined values as schema fields.
