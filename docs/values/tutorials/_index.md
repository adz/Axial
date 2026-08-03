---
weight: 60
title: Result Tutorials
description: Tutorials for pure fail-fast constraints and typed Result values.
---

# Result Tutorials

These tutorials stay in pure F# code. Use them when one failure should stop the operation.

## Guides

- [Constraint and Result](./constraint-result/): attach domain errors to `Violation` values, then compose with `result {}`.

Move to [Schema tutorials]({{< relref "/schema/tutorials/" >}}) when independent fields should report all sibling
failures together.
