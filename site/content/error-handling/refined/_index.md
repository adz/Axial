---
weight: 40
title: Refined
type: docs
description: Type-safe boundaries with Parse, Refine, and the refine {} builder.
---


Plain primitives often allow values your domain rejects: a blank name still fits in `string`, and zero still fits in
`int`. Checking at the boundary helps, but the primitive type does not show that the check happened.

A refined type records that fact. A `PositiveInt` or `NonBlankString` can only be built after its rule passes, so code
receiving the refined value does not need to repeat the check.

Refined values live in ErrorHandling and can be used on their own. Schema can also use them as model fields through
`Schema.refine`.

`Parse` reads primitive input, `Refine` builds a refined value, and `refine {}` connects several dependent parsing
and refinement steps.

## Install

Install Refined on its own:

```sh
dotnet add package Axial.Refined
```

## Mental Model

```text
Untrusted Input -> Parse -> Refine -> Strongly-Typed Value
```

By parsing and refining values before executing core business logic, you prevent invalid states from corrupting your domain model.

## Tutorial

Start at a boundary where everything is still a string:

`let!` takes the value from a successful parse or refinement. `do!` runs a step returning `unit`.
`return!` uses another refinement result as the result of the block.

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

```fsharp
refine {
    let! (parsedId: int) = (Parse.int rawId: Result<int, ParseError>)
    let! (id: NonZeroInt) =
        (Refine.nonZeroInt parsedId: Result<NonZeroInt, RefinementError>)

    return { ... }
}
// Result<Product, RefinementError>
```

After this function succeeds, the `Product` fields hold refined types instead of unchecked primitives.

`refine {}` is fail-fast. Use it when later checks depend on earlier values or when the first error is enough. Use `validate {}` from `Axial.Validation` when independent sibling fields should all report diagnostics at once.

## Guides

- [Tutorials](./tutorials/): parse strings into refined values and a caller-owned domain type.
- [Refine Builder](./refine-builder/): fail-fast parsing and refinement with `refine {}`.
- [Refined Catalog](./catalog/): built-in numeric, text, collection, temporal, character, and choice helpers.
- [Domain Values](./domain-values/): author caller-owned refined values for standalone use and schema fields.

## Reference

- [Refined API]({{< relref "/error-handling/reference/refined/" >}})
