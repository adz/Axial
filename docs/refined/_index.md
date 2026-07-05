---
weight: 25
title: Refined
type: docs
description: Type-safe boundaries with Parse, Refine, and the refine {} builder.
aliases:
  - /docs/refined/
  - /parse/refined/
---

# Refined

Most domain bugs sneak in through primitives. A `string` that must never be blank, an `int` that must be positive, a
slug that must match a format — the type system happily accepts the invalid versions of all of them, so every function
that touches the value re-checks it defensively, or worse, trusts it and breaks later. Validating at the boundary
helps, but a plain `string` that "was validated somewhere" carries no proof: nothing stops unvalidated data from
reaching the same code path.

Refined values fix this by making the proof part of the type. A `PositiveInt` or `NonBlankString` can only be
constructed through a check that succeeded, so once your domain model holds one, every downstream function can trust
it without re-checking. This section shows how to parse untrusted boundary data into those stronger values before the
data reaches your domain model.

This is machinery behind [Schema](../): for whole models, refined values usually arrive as schema
fields (`Value.refined`), and `Input.parse` runs the parsing for you. Come here directly for single values or when
building the domain value types your schemas will use.

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

- [Tutorials](./tutorials/): parse strings into refined values and a caller-owned domain type.
- [Refine Builder](./refine-builder/): fail-fast parsing and refinement with `refine {}`.
- [Refined Catalog](./catalog/): built-in numeric, text, collection, temporal, character, and choice helpers.
- [Domain Values](./domain-values/): author caller-owned refined values for standalone use and schema fields.

## Reference

- [Refined API]({{< relref "/reference/refined/" >}})
