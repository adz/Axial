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

# Error Handling

F# already has the right type for fail-fast logic: `Result<'value, 'error>` with your own error union. The problem is
what happens around it. Real validation code fills up with the same boilerplate — null and blank guards, option
unwrapping, boolean conditions hand-rolled into `Error` branches — and each team invents its own helpers for it.
Reaching for a validation framework, a custom result type, or an effect system fixes the boilerplate, but it's a bad
trade when all you needed was to check a string and stop at the first failure — now every caller signs up for a
bigger dependency than the problem warranted.

This section is the small end of that trade: keep plain `Result` and make it terse. Standard F# `Result` with a
small error union is idiomatic Axial here, not a stopgap before something heavier — Axial has heavier tools
elsewhere (see [Schema]({{< relref "/schema/" >}}) and [Flow]({{< relref "/flow/" >}})), but reaching for them isn't
the point of this section. Three pieces do the work:

- **`Predicate`** — plain `bool` facts, for local branching (`if`, `match`, guard clauses).
- **`Check`** — the same facts as reusable, named, structured checks that return `Result<'value, CheckFailure list>`.
- **`Result`'s focused helpers and the `result {}` builder** — attach domain errors and compose fail-fast steps over
  standard `Result`.

Your domain code stays plain F# that any teammate can read; nothing here changes your function signatures.

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

## Reference

- [Predicate API]({{< relref "/reference/predicate/" >}})
- [Check API]({{< relref "/reference/check/" >}})
- [Result builder API]({{< relref "/reference/result/" >}})

## Comparisons

- [Replacing FsToolkit.ErrorHandling](./fstoolkit-comparison/): the migration path for existing railway-oriented code.
