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

Each field carries a `SchemaDescription` — shape, declared format, and constraint metadata:

```fsharp
let email = description.Fields |> List.find (fun field -> field.Name = "email")

email.Schema.Format          // Some SchemaFormat.email (when declared with Schema.withFormat)

match email.Schema.Shape with
| SchemaShape.Primitive kind -> printfn "primitive %A" kind
| SchemaShape.Refined underlying -> printfn "refined over %A" underlying.Shape
| SchemaShape.Nested model -> printfn "nested with %d fields" model.Fields.Length
| SchemaShape.Many item -> printfn "collection of %A" item.Shape
| SchemaShape.Union union -> printfn "union with cases %A" (union.Cases |> List.map _.Tag)
```

Refined values expose their raw representation through `SchemaShape.Refined`, so a boundary interpreter can render an
`Email` field as a constrained string without knowing the domain type.

## Understand The Constraint Types

Schema authoring uses `SchemaConstraint<'value>`, which prevents attaching a string constraint to an integer schema.
`Constraint.fromCheck` creates one from a complete `Axial.Check.Constraint<'value>`. After Schema combines differently
typed fields into one model description, inspectors see the non-generic `Axial.Schema.ConstraintDescriptor`. The
`Constraint` module creates and inspects Schema constraints; it is not another constraint value type.

`required` and `optional` describe boundary presence before a typed value exists. Other constructors retain complete
Check constraints. All descriptors expose the same stable code and metadata inspection surface.

## Lower Constraints To Another Format

Constraint metadata is the discriminated-union `ConstraintMetadata` vocabulary owned by
[Axial.Check]({{< relref "/error-handling/check/constraints/" >}}), so lowering is one `match`:

```fsharp
let jsonKeyword (constraint': ConstraintDescriptor) =
    match constraint'.Metadata with
    | ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.MaxLength maximum) ->
        Some $"\"maxLength\":{maximum}"
    | ConstraintMetadata.ValueConstraint(Axial.Check.ConstraintMetadata.Pattern pattern) ->
        Some $"\"pattern\":\"{pattern}\""
    | ConstraintMetadata.Presence Presence.Required ->
        None // handled at the object level
    | _ -> None
```

The repository keeps three worked prototypes — a JSON Schema emitter, a docs describer, and a UI metadata producer —
in `tests/Axial.Schema.Tests/SchemaInterpreterPrototypeTests.fs`, all built only on `Inspect`.

## Why This Matters

One declaration now serves parsing, validation, rules, and every metadata consumer. When a constraint changes, the
form control, the JSON Schema, the docs table, and the parser all change together.

## Next

- [Refined Value Schemas](../../refined-values/) for how refinement layers stay inspectable.
- [Schema reference]({{< relref "/schema/reference/schema/" >}}) for the full `Inspect` API.
