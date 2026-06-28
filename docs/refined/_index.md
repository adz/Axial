---
weight: 25
title: Refined
type: docs
description: Type-safe boundaries with Parse, Refine, and the refine {} builder.
---

# Refined

Use this section for turning untrusted boundary data (like raw strings, external JSON, or configuration input) into stronger structural values like `NonBlankString`, `PositiveInt`, or `NonEmptyList` using `Parse`, `Refine`, and the `refine {}` builder.

Use this section when you want to enforce domain invariants right at your application boundaries.

## Mental Model

```text
Untrusted Input -> Parse -> Refine -> Strongly-Typed Value
```

By parsing and refining values before executing core business logic, you prevent invalid states from corrupting your domain model.

## Start Here

- [Refine Builder](./refine-builder/): fail-fast parsing and refinement with `refine {}`.

## Reference

- [Refined API]({{< relref "/reference/refined/" >}})
