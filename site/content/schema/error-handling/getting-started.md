---
weight: 2
title: Getting Started
description: Choosing among Result helpers, Check, Validation, Refined, and Predicate.
type: docs
---


Start with the return type that communicates the behavior your function needs. `Axial.ErrorHandling` offers several
related tools, but an application does not need to adopt all of them.

`Check` is a useful starting point when a constraint should be named, reused, or inspected. It preserves the checked
value on success and reports structured `CheckFailure` values on failure:

```fsharp
open Axial

let checkName (name: string) =
    name |> Check.minLength 3

let result = checkName "Ad"
// Error [MinLength (3, 2)]
```

That failure can remain structural, be used to construct a [refined value](./refined/), or be translated at a domain
or presentation boundary. If a function exposes its own error union, `Result.orError` replaces the check failures;
`Result.mapError` can preserve more detail by translating them.

## Choose by behavior

| You need | Reach for | Shape |
| --- | --- | --- |
| A reusable value constraint | [`Check`](./checks/) | `Result<'value, CheckFailure list>` |
| A type that records successful construction | [`Refined`](./refined/) | A refined value type |
| All independent failures, with locations | [`Validation`](./validation/) | `Validation<'value, 'error>` |
| Fail-fast composition over ordinary F# results | [`Result` helpers and `result {}`](./result-builder/) | `Result<'value, 'error>` |
| A local fact for an `if` or `match` | [`Predicate`](./predicates/) | `bool` |

The tools interoperate, but the table is not a ladder. For example, a `Check` can feed a Result-returning function,
while a `Validation` block can accumulate several existing Results. Use the smallest shape that preserves the
semantics callers need.

## Guides

- [Result Builder](./result-builder/) covers `result {}` for sequencing dependent fail-fast steps.
- [Checks](./checks/) covers the full `Check` surface, the `CheckDSL`, and composition with `Check.all`/`Check.any`.
- [Validation](./validation/) covers accumulated failures and structured diagnostics.
- [Refined](./refined/) covers types constructed from checked values.
- [Predicates](./predicates/) covers `bool` facts for local branching.
- The [tutorial](./tutorials/) walks through building a small validation flow end to end.
