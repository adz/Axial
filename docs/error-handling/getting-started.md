---
weight: 10
title: Getting Started
description: Use Result, Check, Parse, and Refined for typed failures and domain construction.
---

# Getting Started

Install the complete error-handling toolkit:

```bash
dotnet add package Axial.ErrorHandling   # Result, Check, Parse, and Refined
```

Or install only the focused packages an application needs:

```bash
dotnet add package Axial.Result    # Result combinators and result { }
dotnet add package Axial.Check     # reusable checks and portable constraints
dotnet add package Axial.Parse     # serialized primitive decoding
dotnet add package Axial.Refined   # invariant-carrying domain values
```

```fsharp
open Axial.Result
open Axial.Check
open Axial.Parse
open Axial.Refined
```

The packages remain focused:

| Concern | API | Result |
| --- | --- | --- |
| Compose dependent failures | `result { }` | `Result<'value,'error>` |
| Test one typed value | `Check<'value>` | `Result<unit, CheckFailure list>` |
| Keep a checked input | `Result.guard` | `Result<'value,'error>` |
| Decode serialized primitives | `Parse.int`, `Parse.guid`, and other parsers | `Result<'value, ParseError>` |
| Construct an invariant-carrying type | `Refinement.create`, `Refine.*` | `Result<'refined, CheckFailure list>` |

## Where Schema fits

These packages handle explicit operations over individual values and compose their failures through ordinary
`Result`. The caller decides which input each failure belongs to and how to represent the application's error type.

[Axial.Schema]({{< relref "/schema/" >}}) is the structured-boundary layer. A `Schema<'model>` declares fields and
constructors, applies Check constraints and refinements at those fields, and returns accumulated `SchemaError` values
with input paths. The same declaration can also drive JSON codecs, JSON Schema, forms, contracts, and inspection.

Use these focused Error Handling packages directly for local functions, domain constructors, and workflows. Use
Schema when an entire form, request, configuration document, or other structured input must become a model with
field-aware diagnostics. The approaches compose: Schema uses constraints and refinements defined independently in
Check and Refined. Start with [Schema Getting Started]({{< relref "/schema/getting-started/" >}}) when that is your
boundary.

## A complete boundary function

```fsharp
type QuantityError =
    | InvalidInteger of ParseError
    | InvalidQuantity of CheckFailure list

let quantity raw =
    result {
        let! parsed = Parse.int raw |> Result.mapError InvalidInteger
        let! quantity = Check.greaterThan 0 parsed |> Result.map (fun () -> parsed) |> Result.mapError InvalidQuantity
        return quantity
    }
```

`Parse.int` changes representation. `Check.greaterThan` admits only positive integers. Mapping both
errors at the bind sites gives the application one deliberate error type.

## Continue

- [Result](./result/)
- [Check](./check/)
- [Parse and Refined](./refined/)
- [Define Refined Types](./refined/domain-values/)
