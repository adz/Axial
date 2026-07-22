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

# Error Handling

`Axial.ErrorHandling` is a meta-package. It has no API of its own; it installs these three packages together:

| Package | Use it for | Documentation |
| --- | --- | --- |
| `Axial.Result` | Fail-fast Results, reusable checks, predicates, and `result {}` | [Result and Check](./result-builder/) |
| `Axial.Diagnostics` | Accumulating independent failures with paths and names | [Validation and Diagnostics](./diagnostics/) |
| `Axial.Refined` | Parsing and constructing values whose types record successful checks | [Refined](./refined/) |

Install the meta-package when a project uses all three. Install an individual package when it only needs one part.
The [Getting Started guide](./getting-started/) has the installation commands and helps choose between them.

Start with `CheckDSL` when you need to test values and return your own errors:

```fsharp
open Axial.ErrorHandling
open Axial.ErrorHandling.CheckDSL

type SignUpError = NameRequired | NameTooShort

let checkName name =
    name
    |> present
    |> orError NameRequired
    |> Result.bind (minLength 3 >> mapError (fun _ -> NameTooShort))
```

Use these packages when ordinary functions need more structure than hand-written guards, but do not need a schema or
an effect runtime. Existing F# and third-party Result helpers continue to compose with them.

The main choices are:

- **`Check`** for reusable value constraints with structured `CheckFailure` values.
- **`Refined`** when a value's type should record that construction succeeded.
- **`Validation` and `Diagnostics`** when independent failures should accumulate with paths or names.
- **`Result` helpers and `result {}`** for fail-fast composition over ordinary F# Result values.
- **`Predicate`** for the underlying `bool` facts when local branching is enough.

These shapes solve different problems rather than forming a required progression. A function may use one of them and
return an ordinary F# `Result`, or expose the more specific type when that communicates useful semantics.

Error Handling, [Schema]({{< relref "/schema/" >}}), and [Flow]({{< relref "/flow/" >}}) are separate entry points.
Schema depends on the focused packages for checks, diagnostics, and refined fields. Error Handling itself needs neither Schema nor
Flow.

## The Check DSL

Open `Axial.ErrorHandling.CheckDSL` in a module that checks several values. It removes the `Check.` and `Result.`
prefixes from the functions used in most check pipelines.

```fsharp
open Axial.ErrorHandling.CheckDSL

let adultAge : Check<int> = atLeast 18
let contactEmail : Check<string> = Check.all [ present; email ]

let requireAdult age = age |> adultAge |> orError AgeInvalid
let requireEmail value = value |> contactEmail |> mapError InvalidEmail
```

The full DSL is small enough to scan:

| Values | Functions |
| --- | --- |
| Presence | `present`, `empty`, `notEmpty` |
| Strings | `minLength`, `maxLength`, `lengthBetween`, `exactLength`, `email`, `matches` |
| Numbers | `greaterThan`, `lessThan`, `atLeast`, `atMost`, `positive`, `nonNegative`, `negative`, `nonPositive` |
| Collections | `minCount`, `maxCount`, `countBetween` |
| General values | `oneOf`, `equalTo`, `notEqualTo` |
| Failures | `mapFailure`, `orError`, `mapError` |

Some names stay qualified because they would hide common F# functions. Use `Check.all`, `Check.any`,
`Check.``not```, `Check.contains`, `Check.distinct`, `Check.length`, and `Check.between`.

The [Check guide](./checks/) explains each group. The [Check API]({{< relref "/error-handling/reference/check/" >}})
lists the qualified modules and every available function.

## Guides

- [Getting Started](./getting-started/): choose a tool from the package and see the smallest useful example.
- [Result Builder](./result-builder/): fail-fast composition over standard `Result` with `result {}`.
- [Checks](./checks/): reusable, named constraints and how they attach to `Result`.
- [Diagnostics](./diagnostics/): accumulate sibling failures into a path-aware diagnostics tree, instead of stopping at
  the first one.
- [Refined](./refined/): construct values whose types record an invariant.
- [Predicates](./predicates/): plain `bool` facts for local branching.
- [Tutorials](./tutorials/): build a small validation flow end to end.
- [Walkthrough: Registration Desk](./reference-app/): the introductory reference app — all four stages of this
  section in one runnable program.

## Reference

- [Predicate API]({{< relref "/error-handling/reference/predicate/" >}})
- [Check API]({{< relref "/error-handling/reference/check/" >}})
- [Result builder API]({{< relref "/error-handling/reference/result/" >}})

## Comparisons

- [Replacing FsToolkit.ErrorHandling](./fstoolkit-comparison/): the migration path for existing railway-oriented code.
