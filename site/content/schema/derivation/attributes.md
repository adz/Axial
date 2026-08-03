---
title: Derivation Attributes
linkTitle: Attributes
description: Complete attribute reference for schema derivation, with handwritten Schema DSL equivalents.
weight: 20
type: docs
---


Open `Axial.Schema.Derive` to use the attributes without suffixes:

```fsharp
open Axial.Schema.Derive
```

Attributes are read from F# source at generation time. They do not use runtime reflection. Each generated operation in
the table has the same meaning as its linked handwritten [Schema DSL](../syntax/) equivalent.

## Type and construction attributes

| Attribute | Applies to | Short description | Schema DSL equivalent |
| --- | --- | --- | --- |
| `[<DeriveSchema>]` | record | Generate a schema and companion operations for a public wire record. `Chain` and `Version` optionally identify a version series. | [`schema<'model> { ... }`](../syntax/#fields-without-blocks) |
| `[<DeriveUnion "kind">]` | union | Derive an internally tagged union; every case carries one marked record payload. | [`Schema.union`](../union-schemas/#define-cases) |
| `[<SchemaName "wire_name">]` | record field or nullary union case | Override the configured wire name or case tag. | [`field "wire_name" _.Field`](../syntax/#fields-without-blocks) |
| `[<SchemaConstructor>]` | static member | Call this member with fields in declaration order instead of constructing a record literal. | [`construct`](../syntax/#constructors) |

## Field attributes

| Attribute | Short description | Schema DSL equivalent |
| --- | --- | --- |
| `[<Pattern "expr">]` | Require text to match a regular expression. | [`constrain (pattern "expr")`](../syntax/#constraint-equivalents) |
| `[<Min n>]` | Set the minimum natural length of text, list, or map. | [`constrain (minLength n)`](../syntax/#constraint-equivalents) |
| `[<Max n>]` | Set the maximum natural length of text, list, or map. | [`constrain (maxLength n)`](../syntax/#constraint-equivalents) |
| `[<Length n>]` | Require an exact natural length. | [`constrain (length n)`](../syntax/#constraint-equivalents) |
| `[<LengthBetween(min, max)>]` | Bound natural length inclusively. | [`constrain (lengthBetween min max)`](../syntax/#constraint-equivalents) |
| `[<Present>]` | Require a string, collection, or optional value to be present/non-empty. | [`constrain present`](../syntax/#constraint-equivalents) |
| `[<Supplied>]` | Require the input object to contain this field key. | [`mustSupply`](../syntax/#constraint-equivalents) |
| `[<Format "name">]` | Attach open format metadata without adding a check. | [`format (SchemaFormat.create "name")`](../syntax/#constraint-equivalents) |
| `[<AtLeast n>]` | Inclusive numeric lower bound (`>=`). | [`constrain (atLeast n)`](../syntax/#constraint-equivalents) |
| `[<GreaterThan n>]` | Exclusive numeric lower bound (`>`). | [`constrain (greaterThan n)`](../syntax/#constraint-equivalents) |
| `[<AtMost n>]` | Inclusive numeric upper bound (`<=`). | [`constrain (atMost n)`](../syntax/#constraint-equivalents) |
| `[<LessThan n>]` | Exclusive numeric upper bound (`<`). | [`constrain (lessThan n)`](../syntax/#constraint-equivalents) |
| `[<MultipleOf n>]` | Require a numeric value to be a whole multiple of `n`. | [`constrain (multipleOf n)`](../syntax/#constraint-equivalents) |
| `[<Distinct>]` | Require all list elements to be distinct. | [`constrain distinct`](../syntax/#constraint-equivalents) |
| `[<Email>]` | Apply the built-in email constraint to text. | [`constrain email`](../syntax/#constraint-equivalents) |
| `[<Default value>]` | Supply a value when the input key is omitted; invalid on `option` fields. | [`defaultValue value`](../syntax/#constraint-equivalents) |

Numeric bound and default attributes accept source literals supported by their target field. The generator reads the
literal text, preserving decimal precision rather than round-tripping it through reflection.

## Example

```fsharp
[<DeriveSchema>]
type Product =
    { [<SchemaName "product_code"; Pattern "^[A-Z0-9]+$"; Present>]
      Code: string
      [<AtLeast 0; LessThan 1000000>]
      Price: decimal
      [<Min 1; Distinct>]
      Tags: string list
      [<Default true>]
      Active: bool }
```

Multiple constraints are emitted in attribute order and execute like multiple `constrain` operations in a field block.
Doc comments on records and fields become schema descriptions and generated XML documentation.
