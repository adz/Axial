---
weight: 2
title: Getting Started
description: Why Schema exists, what it describes, and how its parts fit together.
---

# Getting Started

This page shows why Axial uses Schema for domain-model boundaries and gives a compact map of the parts.

## The Problem Schema Solves

A validator starts with an object. That is often too late: the object may already violate the rules that give the
model its meaning, and the type does not record whether validation ran. Boundary code then repeats the same work in
several forms:

- parse strings and JSON values;
- accumulate failures without losing field paths;
- retain rejected input for a form or editor;
- construct the model only after every field is acceptable;
- describe the same constraints to codecs, JSON Schema, documentation, and tests.

A `Schema<'model>` makes that boundary explicit. It records how fields are named, parsed, constrained, ordered, read,
and passed to the model constructor. `Model.parse` runs untrusted `RawInput` through that declaration. If any field
or constructor invariant fails, no model is returned. The failure is a path-aware diagnostics graph and the original
input remains available.

This is appropriate for domain models and boundary records. For a small function with one or two ordinary failure
cases, plain F# `Result` with an application-owned error type is simpler.

## A First Schema

```fsharp
open Axial.Schema
open Axial.Validation

type Signup =
    { Email: string
      Age: int }

let signupSchema =
    // recordFor anchors Signup before the first field. The constructor is checked
    // against field order and types, so the schema cannot silently omit an argument.
    Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
    // Constraints are data as well as executable checks. Other interpreters can read them.
    |> Schema.fieldWith [ SchemaConstraint.email ] "email" _.Email Value.text
    |> Schema.fieldWith [ SchemaConstraint.between 13 120 ] "age" _.Age Value.int
    |> Schema.build

let raw =
    RawInput.ofNameValues
        [ "email", "ada@example.com"
          "age", "36" ]

match (Model.parse signupSchema raw).Result with
| Ok signup ->
    // The constructor ran only after both fields parsed and passed their constraints.
    printfn "%s" signup.Email
| Error diagnostics ->
    // Diagnostics retain paths such as email and age; ParsedInput also retains raw.
    printfn "%A" (Diagnostics.flatten diagnostics)
```

The qualified form above keeps the vocabulary visible. `Axial.Schema.DSL` provides the same builder operations
without prefixes inside a schema-definition module; it does not define a second schema model.

## The Parts

| Part | Role |
| --- | --- |
| `Schema` and `Value` | Declare records, primitive and domain values, nesting, collections, maps, options, unions, and recursion. |
| `SchemaConstraint` and `SchemaFormat` | Attach portable checks and descriptive boundary metadata. |
| `RawInput`, `Model.parse`, and `ParsedInput` | Turn source-neutral boundary data into a model or path-aware diagnostics while retaining rejected input. |
| `Model.validate` and `Model.reconstruct` | Establish schema trust for named-field drafts or already-existing model values. |
| `Model<'model>` | Record the trust claim in a type when application code must distinguish drafts from accepted values. |
| `FieldRef` and `ContextRules` | Name, read, and copy-update fields; attach contextual failures to stable schema paths. |
| `Inspect` and `JsonSchema` | Read the declaration as finite metadata and emit JSON Schema, including recursive references. |
| `Axial.Codec.Json` | Compile a reflection-free JSON hot path for already-trusted data. Codec decoding checks wire shape; use Schema parsing for full constraint diagnostics. |
| `Contract` | Select explicit wire versions, parse frozen schemas, and compose typed migrations into the current trusted model. |
| `.contract` and `schemagen` | Generate repetitive wire records, schemas, parsers, validation functions, and field references from a checked declaration file. |
| `Axial.Schema.Testing.SchemaGen` | Derive FsCheck generators through `RawInput`; unsupported constraints require an explicit field generator. |

The [Schema overview examples](./overview-examples/) exercise each part and state when it is appropriate. The other
guides go into individual APIs; this page stops at the system boundary so the overall shape can be reviewed first.

## Related

- Use [Error Handling]({{< relref "/error-handling/" >}}) for plain `Result`, reusable checks, diagnostics machinery,
  and refined single values.
- Use [Flow]({{< relref "/flow/" >}}) for dependencies, asynchronous work, cancellation, and typed operational
  failures around an accepted model.
