---
weight: 20
title: Error Handling
type: docs
description: Pure fail-fast logic with Check and standard F# Result.
aliases:
  - /docs/validation-results/
---

# Error Handling

This section is one of Axial's two doors: **plain `Result` for simple code**. Standard F# `Result<'value, 'error>`
with your own error union is idiomatic Axial; `Check`, the focused `Result` helpers, and the `result {}` builder are
the machinery that keeps it terse. (The other door is [Schema](../schema/), for whole domain models.)

Use this section when the code is still pure and one failure is enough to stop the operation. Do not introduce `Flow` just because dependencies might appear later.

## Mental Model

```text
Check -> Result
```

`Check` gives reusable structured value checks. `Result` preserves inputs, extracts inner values, adds typed failures, and composes fail-fast steps. The output is still ordinary `Result`, so the rest of your domain code stays plain F#.

## Start Here

- [Tutorials](./tutorials/): build typed `Result` values from pure checks.
- [Checks](./checks/): structured checks and how to move into typed `Result` values.
- [Result Builder](./result-builder/): fail-fast composition over standard `Result`.

## Move On When

- Need sibling failures accumulated together? Move to [Validation](../validation/).
- Need async, task work, dependencies, cancellation, resources, or runtime policy? Move to [Flow](../flow/).

## Reference

- [Check API]({{< relref "/reference/check/" >}})
- [Result builder API]({{< relref "/reference/result/" >}})
