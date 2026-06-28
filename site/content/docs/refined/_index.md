---
weight: 25
title: Refined
type: docs
description: Type-safe boundaries with Parse, Refine, and the refine {} builder.
---


This page shows how to turn untrusted boundary data into stronger F# values before the data reaches your domain model.

Use `Parse` for serialized primitive input, `Refine` for built-in refined values, submodules such as `Text`, `Numeric`, `Collection`, `Temporal`, and `Choice` for discoverability, and `refine {}` to sequence fail-fast construction.

## Mental Model

```text
Untrusted Input -> Parse -> Refine -> Strongly-Typed Value
```

By parsing and refining values before executing core business logic, you prevent invalid states from corrupting your domain model.

## Tutorial

Start at a boundary where everything is still a string:

```fsharp
open Axial.Refined

type ProductId = ProductId of NonZeroInt
type ProductSlug = ProductSlug of Slug
type Quantity = Quantity of PositiveInt

type Product =
    { Id: ProductId
      Slug: ProductSlug
      Quantity: Quantity }

let createProduct rawId rawSlug rawQuantity : Result<Product, RefinementError> =
    refine {
        let! parsedId = Parse.int rawId
        let! id = Refine.nonZeroInt parsedId
        let! slug = Refine.slug rawSlug
        let! parsedQuantity = Parse.int rawQuantity
        let! quantity = Refine.positiveInt parsedQuantity

        return {
            Id = ProductId id
            Slug = ProductSlug slug
            Quantity = Quantity quantity
        }
    }
```

After this function succeeds, `Product` cannot contain `0` as an id, a malformed slug, or a non-positive quantity unless code deliberately unwraps and bypasses the refined values.

`refine {}` is fail-fast. Use it when later checks depend on earlier values or when the first error is enough. Use `validate {}` from `Axial.Validation` when independent sibling fields should all report diagnostics at once.

## Start Here

- [Refine Builder](./refine-builder/): fail-fast parsing and refinement with `refine {}`.
- [Refined Catalog](./catalog/): built-in numeric, text, collection, temporal, character, and choice helpers.

## Reference

- [Refined API]({{< relref "/reference/refined/" >}})
