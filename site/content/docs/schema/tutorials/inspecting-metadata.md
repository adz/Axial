---
weight: 25
title: Inspecting Schema Metadata Tutorial
description: Walk the same schema as data for docs and JSON Schema.
type: docs
---


A schema is data, not a validator. This tutorial reads a schema's fields, shapes, formats, and constraints without
parsing any input or constructing any model — the foundation for JSON Schema emitters, documentation generators, and
UI metadata.

## Describe A Schema

`Inspect.model` turns a built schema into a plain metadata tree:

```fsharp
open Axial.Schema

let description = Inspect.model signupSchema

description.Fields |> List.map _.Name    // ["email"; "age"]
description.Fields |> List.map _.Order   // [0; 1]
```

Nothing executes: no getters run, no constructors are called, no checks fire. The description is immutable data.

## Read Field Shapes And Constraints

Each field carries a `ValueDescription` — shape, declared format, and constraint metadata:

```fsharp
let email = description.Fields |> List.find (fun field -> field.Name = "email")

email.Value.Format          // Some SchemaFormat.email (when declared with Value.withFormat)

match email.Value.Shape with
| ValueShape.Primitive kind -> printfn "primitive %A" kind
| ValueShape.Refined underlying -> printfn "refined over %A" underlying.Shape
| ValueShape.Nested model -> printfn "nested with %d fields" model.Fields.Length
| ValueShape.Many item -> printfn "collection of %A" item.Shape
```

Refined values expose their raw representation through `ValueShape.Refined`, so a boundary interpreter can render an
`Email` field as a constrained string without knowing the domain type.

## Lower Constraints To Another Format

Constraint metadata is a closed, typed vocabulary (`SchemaConstraintMetadata`), so lowering is one `match`:

```fsharp
let jsonKeyword (constraint': SchemaConstraint) =
    match constraint'.Metadata with
    | SchemaConstraintMetadata.MaxLength maximum -> Some $"\"maxLength\":{maximum}"
    | SchemaConstraintMetadata.Pattern pattern -> Some $"\"pattern\":\"{pattern}\""
    | SchemaConstraintMetadata.Required -> None   // handled at the object level
    | _ -> None
```

The repository keeps three worked prototypes — a JSON Schema emitter, a docs describer, and a UI metadata producer —
in `tests/Axial.Schema.Tests/SchemaInterpreterPrototypeTests.fs`, all built only on `Inspect`.

## Why This Matters

One declaration now serves parsing, validation, rules, and every metadata consumer. When a constraint changes, the
form control, the JSON Schema, the docs table, and the parser all change together.

## Next

- [Refined Value Schemas](../../refined-values/) for how refinement layers stay inspectable.
- [Schema reference]({{< relref "/reference/schema/" >}}) for the full `Inspect` API.
