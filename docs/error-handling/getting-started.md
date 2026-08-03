---
weight: 10
title: Getting Started
description: Use Result, Constraint, Parse, and Refined for typed failures and domain construction.
---

# Getting Started

Install the complete error-handling toolkit:

```bash
dotnet add package Axial.ErrorHandling   # Result, Constraint, Parse, and Refined
```

Or install only the focused packages an application needs:

```bash
dotnet add package Axial.Result    # Result combinators and result { }
dotnet add package Axial.Constraint     # reusable checks and portable constraints
dotnet add package Axial.Parse     # serialized primitive decoding
dotnet add package Axial.Refined   # invariant-carrying domain values
```

```fsharp
open Axial.Result
open Axial.Constraint
open Axial.Parse
open Axial.Refined
```

The packages remain focused:

| Concern | API | Result |
| --- | --- | --- |
| Compose dependent failures | `result { }` | `Result<'value,'error>` |
| Test one typed value | `Constraint<'value>` | `Result<unit, Violation>` |
| Keep a checked input | `Result.guard` | `Result<'value,'error>` |
| Decode serialized primitives | `Parse.int`, `Parse.guid`, and other parsers | `Result<'value, ParseError>` |
| Construct an invariant-carrying type | `Refinement.create`, `Refine.*` | `Result<'refined, Violation>` |

## Where Schema fits

These packages handle explicit operations over individual values and compose their failures through ordinary
`Result`. The caller decides which input each failure belongs to and how to represent the application's error type.

[Axial.Schema]({{< relref "/schema/" >}}) is the structured-boundary layer. A `Schema<'model>` declares fields and
constructors, applies constraints and refinements at those fields, and returns accumulated `SchemaError` values
with input paths. The same declaration can also drive JSON codecs, JSON Schema, forms, contracts, and inspection.

Use these focused Error Handling packages directly for local functions, domain constructors, and workflows. Use
Schema when an entire form, request, configuration document, or other structured input must become a model with
field-aware diagnostics. The approaches compose: Schema uses constraints and refinements defined independently in
Constraint and Refined. Start with [Schema Getting Started]({{< relref "/schema/getting-started/" >}}) when that is your
boundary.

## A complete boundary function

```fsharp
type QuantityError =
    | InvalidInteger of ParseError
    | InvalidQuantity of Violation

let quantity raw =
    result {
        let! parsed = Parse.int raw |> Result.mapError InvalidInteger
        let! quantity = parsed |> Constraint.guard (Constraint.greaterThan 0) |> Result.mapError InvalidQuantity
        return quantity
    }
```

`Parse.int` changes representation. `Constraint.greaterThan` admits only positive integers. Mapping both
errors at the bind sites gives the application one deliberate error type.

Render a constraint failure when it crosses into text:

```fsharp
match quantity "0" with
| Ok quantity -> printfn "Quantity: %d" quantity
| Error (InvalidInteger error) -> printfn "Invalid integer: %A" error
| Error (InvalidQuantity violation) -> printfn "Invalid quantity: %s" (Violation.render violation)
```

Keep the `Violation` structured inside `QuantityError`; the UI, log, or HTTP boundary decides when and how to render
it. See [Working with violations](./constraint/violations/) for grouped failures, inspection, and localization.

## Continue

- [Result](./result/)
- [Constraint](./constraint/)
- [Working with violations](./constraint/violations/)
- [Parse and Refined](./refined/)
- [Define Refined Types](./refined/domain-values/)
