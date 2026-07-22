---
weight: 5
title: Refined Tutorials
description: Tutorials for parsing and refining boundary values into domain types.
type: docs
---


These tutorials use `Parse`, `Refine`, and `refine {}` to turn untrusted boundary values into typed domain values,
preferring "parse" over "error handling".

## Guides

- [Order Reference](./order-reference/): parse strings into refined values and a caller-owned domain type.

Use [Schema tutorials]({{< relref "/schema/tutorials/" >}}) when the input is a whole model rather than individual values, and
[Error Handling tutorials]({{< relref "/validation/tutorials/" >}}) for plain fail-fast checks.
