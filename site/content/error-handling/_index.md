---
weight: 6
title: Error Handling
type: docs
description: Pure fail-fast logic with Check and standard F# Result.
menu:
  main:
    weight: 4
---


F# already has the right type for fail-fast logic: `Result<'value, 'error>` with your own error union. The problem is
what happens around it. Real validation code fills up with the same boilerplate — null and blank guards, option
unwrapping, boolean conditions hand-rolled into `Error` branches — and each team invents its own helpers for it. Worse,
the ecosystem often answers this small problem with a big hammer: a validation framework, a custom result type, or an
effect system, when all you needed was to check a string and stop at the first failure.

This section is Axial's answer: **keep plain `Result` and make it terse**. Standard F# `Result` with a small error
union is idiomatic Axial, not a compromise — `Check`, the focused `Result` helpers, and the `result {}` builder are the
machinery that removes the boilerplate without changing your signatures. Your domain code stays plain F# that any
teammate can read. It is one of the three areas Axial consists of — each usable independently, all working together; the others are [Schema]({{< relref "/schema/" >}}) for whole domain models and [Flow]({{< relref "/flow/" >}}) for effects.

Use this section when the code is still pure and one failure is enough to stop the operation. Do not introduce `Flow` just because dependencies might appear later.

## Mental Model

```text
Check -> Result
```

`Check` gives reusable structured value checks. `Result` preserves inputs, extracts inner values, adds typed failures, and composes fail-fast steps. The output is still ordinary `Result`, so the rest of your domain code stays plain F#.

When a check participates in schema boundary parsing, its `CheckFailure` values lower into `SchemaError` and render with
the same display layer as parse, refinement, validation, and rule failures. For simple application code, keep mapping
checks into your own error DU with one `Result.mapError` function.

## Start Here

- [Tutorials](./tutorials/): build typed `Result` values from pure checks.
- [Checks](./checks/): structured checks and how to move into typed `Result` values.
- [Result Builder](./result-builder/): fail-fast composition over standard `Result`.

## Move On When

- Need sibling failures accumulated together? Move to [Validation]({{< relref "/schema/validation/" >}}).
- Need async, task work, dependencies, cancellation, resources, or runtime policy? Move to [Flow]({{< relref "/flow/" >}}).

## Reference

- [Check API]({{< relref "/reference/check/" >}})
- [Result builder API]({{< relref "/reference/result/" >}})
