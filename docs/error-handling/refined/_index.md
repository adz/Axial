---
weight: 40
title: Refined
type: docs
description: Type-safe boundaries with Parse, Refine, and the refine {} builder.
---

# Refined

Plain primitives often allow values your domain rejects: a blank name still fits in `string`, and zero still fits in
`int`. Checking at the boundary helps, but the primitive type does not show that the check happened.

A refined type records that fact. A `PositiveInt` or `NonBlankString` can only be built after its rule passes, so code
receiving the refined value does not need to repeat the check.

Refined values live in ErrorHandling and can be used on their own.

`Refine.from` is the type-directed entry point. Its source type and expected result type determine the parser or
refined constructor at compile time:

```fsharp
let id : Result<int, RefinementError> =
    Refine.from rawId

let quantity : Result<PositiveInt, RefinementError> =
    Refine.from parsedQuantity
```

Named `Parse` and `Refine` functions handle operations that require parameters or share the same source and destination
types. `refine {}` applies type-directed refinement at each `let!` while sequencing dependent steps.

## Install

Install Refined on its own:

```sh
dotnet add package Axial.Refined
```

## Mental Model

```text
Untrusted Input -> Parse -> Refine -> Strongly-Typed Value
```

## Define your own refinement

Your destination type joins `Refine.from` by defining a static `RefineFrom` member:

```fsharp
type CustomerId =
    private
    | CustomerId of PositiveInt

    static member RefineFrom(raw: string, _: CustomerId) : Result<CustomerId, RefinementError> =
        refine {
            let! (parsed: int) = raw
            let! (positive: PositiveInt) = parsed
            return CustomerId positive
        }

let customerId : Result<CustomerId, RefinementError> =
    Refine.from rawCustomerId
```

Define one `RefineFrom` member for each source and destination pair. Two interpretations with the same pair have no
type-level distinction; expose them as named functions such as `CustomerId.fromHexText`.

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
        let! (parsedId: int) = rawId
        let! (id: NonZeroInt) = parsedId
        let! (slug: Slug) = rawSlug
        let! (parsedQuantity: int) = rawQuantity
        let! (quantity: PositiveInt) = parsedQuantity

        return {
            Id = ProductId id
            Slug = ProductSlug slug
            Quantity = Quantity quantity
        }
    }
```

```fsharp
refine {
    let! (parsedId: int) = (rawId: string)
    let! (id: NonZeroInt) = (parsedId: int)

    return { ... }
}
// Result<Product, RefinementError>
```

After this function succeeds, the `Product` fields hold refined types instead of unchecked primitives.

`refine {}` stops at the first failure, so a later step can depend on every earlier success. `validate {}` from
`Axial.Validation` runs independent sibling fields and reports all of their diagnostics together.

## Guides

- [Tutorials](./tutorials/): parse strings into refined values and your own domain type.
- [Refine Builder](./refine-builder/): fail-fast parsing and refinement with `refine {}`.
- [Refined Catalog](./catalog/): built-in numeric, text, collection, temporal, character, and choice helpers.
- [Domain Values](./domain-values/): define your own refined values for standalone use.
- [Relation to Schema](./schema/): use refined values as fields in an `Axial.Schema` model.

## Reference

- [Refined API]({{< relref "/error-handling/reference/refined/" >}})
