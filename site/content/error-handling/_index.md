---
title: "Error Handling: Result, diagnostics, and refined values"
linkTitle: Error Handling
type: docs
notoc: true
description: Helpers for constraints, fail-fast results, accumulated diagnostics, and refined values.
weight: 6
menu:
  main:
    weight: 4
---


`Axial.ErrorHandling` is a meta-package. It has no API of its own; it installs these three packages together:

| Package | Use it for | Documentation |
| --- | --- | --- |
| `Axial.Result` | Fail-fast Results, reusable checks, predicates, and `result {}` | [Result](./result/) |
| `Axial.Diagnostics` | Accumulating independent failures with paths and names | [Validation and Diagnostics](./diagnostics/) |
| `Axial.Refined` | Parsing and constructing values whose types record successful checks | [Refined](./refined/) |

Install the meta-package when a project uses all three. Install an individual package when it only needs one part.
The [Getting Started guide](./getting-started/) has the installation commands and helps choose between them.

## `Axial.Result`

Use `Axial.Result` for ordinary F# `Result` workflows where the first failure stops the operation. It provides:

- `Check<'value>` programs for reusable value constraints
- `Axial.ErrorHandling.CheckDSL` for concise check pipelines
- focused `Result` helpers for guards, extraction, conversion, and traversal
- the `result {}` computation expression for dependent fail-fast steps
- `Predicate` functions when a local branch only needs a `bool`

Open `CheckDSL` in a module that defines several checks. A passing check returns the original value, so checks can
attach application errors and bind directly inside `result {}`:

```fsharp
open Axial.ErrorHandling
open Axial.ErrorHandling.CheckDSL

type SignUpError =
    | NameRequired
    | NameTooShort of CheckFailure list
    | EmailInvalid of CheckFailure list

let signUp name emailAddress =
    result {
        let! name = name |> present |> orError NameRequired
        let! name = name |> minLength 3 |> mapError NameTooShort
        let! emailAddress = emailAddress |> email |> mapError EmailInvalid
        return name, emailAddress
    }
```

Some names stay qualified because they would hide common F# functions. Use `Check.all`, `Check.any`,
`Check.``not```, `Check.contains`, `Check.distinct`, `Check.length`, and `Check.between`.

Go deeper:

- [Result](./result/): the package overview.
- [Checks](./checks/): the Check DSL, composition, and the complete constraint catalog.
- [Result Builder](./result-builder/): dependent fail-fast steps with `result {}`.
- [Predicates](./predicates/): plain `bool` facts for local branching.
- API reference: [Result]({{< relref "/error-handling/reference/result/" >}}),
  [Check]({{< relref "/error-handling/reference/check/" >}}), and
  [Predicate]({{< relref "/error-handling/reference/predicate/" >}}).

## `Axial.Diagnostics`

Use `Axial.Diagnostics` when independent checks should all run and report every failure. It provides
`Validation<'value, 'error>`, the path-aware `Diagnostics<'error>` error tree, and the `validate {}` computation
expression.

Use `and!` for independent fields and `validate.name` to put each failure at its field path:

```fsharp
open Axial.Validation

let validateRegistration name email =
    validate {
        let! name =
            validate.name "name" {
                return! validateName name
            }

        and! email =
            validate.name "email" {
                return! validateEmail email
            }

        return name, email
    }
```

If both fields fail, the result contains one Diagnostics tree with separate `name` and `email` branches. Flatten it
only at a boundary that needs a list:

```fsharp
validateRegistration name email
|> Validation.toResult
|> Result.mapError Diagnostics.flatten
```

Go deeper:

- [Validation and Diagnostics](./diagnostics/): the package overview.
- [Validate Builder](./diagnostics/validate-builder/): accumulation with `validate {}` and `and!`.
- [Diagnostics trees](./diagnostics/diagnostics/): paths, merging, flattening, and rendering.
- [Validation API]({{< relref "/error-handling/reference/validation/" >}}) and [Diagnostics API]({{< relref "/error-handling/reference/diagnostics/" >}}).

## `Axial.Refined`

Use `Axial.Refined` at boundaries where a successful check should be visible in the value's type. It provides
primitive parsers, built-in refined types and constructors, and the `refine {}` computation expression. The builder
parses or refines raw values according to the type on the left side of `let!`. It also binds explicit `Parse.*` and
`Refine.*` results without manual error conversion.

```fsharp
open Axial.Refined

type ProductName = ProductName of NonBlankString
type Quantity = Quantity of PositiveInt

let createLine rawId rawName rawQuantity : Result<int * ProductName * Quantity, RefinementError> =
    refine {
        let! (id: int) = rawId
        let! (name: NonBlankString) = rawName
        let! (quantity: PositiveInt) = rawQuantity
        return id, ProductName name, Quantity quantity
    }
```

The left-hand types direct `rawId` through `Parse.int`, `rawName` through `Refine.nonBlankString`, and `rawQuantity`
through `Refine.positiveInt`. After the function succeeds, callers receive parsed and refined values rather than
unchecked input.

Go deeper:

- [Refined](./refined/): the package overview and first complete example.
- [Refine Builder](./refined/refine-builder/): dependent parsing and construction with `refine {}`.
- [Refined Catalog](./refined/catalog/): built-in text, numeric, collection, temporal, character, and choice types.
- [Refined API]({{< relref "/error-handling/reference/refined/" >}}).

## How the packages fit together

The packages compose but do not form a required progression. Use `Axial.Result` for fail-fast operations,
`Axial.Diagnostics` for independent failures that should accumulate, and `Axial.Refined` when successful
construction should change the value's type. Install only the package whose behavior the caller needs.

Error Handling, [Schema]({{< relref "/schema/" >}}), and [Flow]({{< relref "/flow/" >}}) are separate entry points.
Schema depends on these focused packages for checks, diagnostics, and refined fields. Error Handling itself needs
neither Schema nor Flow.

For one end-to-end example, use the [Registration Desk walkthrough](./reference-app/). The
[tutorials](./tutorials/) build the same ideas in smaller steps.

## Comparisons

- [Replacing FsToolkit.ErrorHandling](./fstoolkit-comparison/): the migration path for existing railway-oriented code.
