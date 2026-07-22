---
weight: 2
title: Getting Started
description: Choosing among Result helpers, Check, Validation, Refined, and Predicate.
type: docs
---


Start with the return type that communicates the behavior your function needs. `Axial.ErrorHandling` offers several
related tools, but an application does not need to adopt all of them.

Start with `Check` when the same rule is used in more than one place. Open `CheckDSL` in a module that contains several
checks:

```fsharp
open Axial
open Axial.ErrorHandling.CheckDSL

let checkName (name: string) =
    name |> minLength 3

let result = checkName "Ad"
// Error [InvalidLength (MinimumLength 3, Some 2)]
```

The result keeps the original value when the check passes and returns `CheckFailure` values when it fails. At the
application boundary, `orError` can replace those details with one error, while `mapError` can carry them forward.

```fsharp
let requireName name = name |> present |> orError NameRequired
let checkAge age = age |> atLeast 18 |> mapError InvalidAge
```

## Choose by behavior

| You need | Reach for | Shape |
| --- | --- | --- |
| A reusable value constraint | [`Check`](./checks/) | `Result<'value, CheckFailure list>` |
| A type that records successful construction | [`Refined`](./refined/) | A refined value type |
| All independent failures, with locations | [`Diagnostics`](./diagnostics/) | `Validation<'value, 'error>` |
| Fail-fast composition over ordinary F# results | [`Result` helpers and `result {}`](./result-builder/) | `Result<'value, 'error>` |
| A local fact for an `if` or `match` | [`Predicate`](./predicates/) | `bool` |

The tools interoperate, but the table is not a ladder. For example, a `Check` can feed a Result-returning function,
while a `Validation` block can accumulate several existing Results. Use the smallest shape that preserves the
semantics callers need.

## Guides

- [Result Builder](./result-builder/) covers `result {}` for sequencing dependent fail-fast steps.
- [Checks](./checks/) covers the full `Check` surface, the `CheckDSL`, and composition with `Check.all`/`Check.any`.
- [Diagnostics](./diagnostics/) covers accumulated failures and structured diagnostics.
- [Refined](./refined/) covers types constructed from checked values.
- [Predicates](./predicates/) covers `bool` facts for local branching.
- The [tutorial](./tutorials/) walks through building a small validation flow end to end.
