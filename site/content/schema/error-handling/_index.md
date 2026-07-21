---
weight: 6
title: Error Handling
type: docs
notoc: true
description: Pure fail-fast logic with Check and standard F# Result.
menu:
  main:
    weight: 4
---


F# already has `Result<'value, 'error>` for functions that either return a value or a known failure. Application code
still repeats the same work around it: blank-string checks, option extraction, Boolean guards, and error mapping.

This package keeps the standard type and removes that repeated plumbing. Use it for small parsing and decision
functions where a whole model schema or workflow runtime would add more structure than the problem needs.

Three pieces do the work:

- **`Predicate`** — plain `bool` facts, for local branching (`if`, `match`, guard clauses).
- **`Check`** — the same facts as reusable, named, structured checks that return `Result<'value, CheckFailure list>`.
- **`Result`'s focused helpers and the `result {}` builder** — attach domain errors and compose fail-fast steps over
  standard `Result`.

Your functions still return ordinary F# `Result` values with your own error type.

This is one of three packages Axial consists of, each usable on its own: this one for single-value fail-fast logic,
[Schema]({{< relref "/schema/" >}}) for whole domain models parsed at a boundary, and [Flow]({{< relref "/flow/" >}})
for effects, dependencies, and runtime policy. The rest of this section stands on its own — it doesn't assume you'll
ever reach for the other two.

## Install

This section is one NuGet package:

```sh
dotnet add package Axial.ErrorHandling
```

`Check`, `Result`, `Predicate`, and `result {}` live here. `Validation` — accumulating sibling failures into a
path-aware diagnostics tree, rather than stopping at the first one — ships in this same package too; see the
[Validation docs](./validation/) for that half of the surface.

## Guides

- [Getting Started](./getting-started/): why `Check` and `Result` exist, and the smallest useful example.
- [Predicates](./predicates/): plain `bool` facts for local branching.
- [Checks](./checks/): reusable, named, structured checks and how they attach to `Result`.
- [Result Builder](./result-builder/): fail-fast composition over standard `Result` with `result {}`.
- [Validation](./validation/): accumulate sibling failures into a path-aware diagnostics tree, instead of stopping at
  the first one.
- [Tutorials](./tutorials/): build a small validation flow end to end.
- [Walkthrough: Registration Desk](./reference-app/): the introductory reference app — all four stages of this
  section in one runnable program.

## Reference

- [Predicate API]({{< relref "/schema/reference/error-handling/predicate/" >}})
- [Check API]({{< relref "/schema/reference/error-handling/check/" >}})
- [Result builder API]({{< relref "/schema/reference/error-handling/result/" >}})

## Comparisons

- [Replacing FsToolkit.ErrorHandling](./fstoolkit-comparison/): the migration path for existing railway-oriented code.
