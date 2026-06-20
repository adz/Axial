---
title: "validate { }"
weight: 2000
type: docs
---

This page shows the `validate { }` computation expression for writing validation logic with direct, sequential syntax. The builder is best for validation steps that read clearly as a block while still returning `Validation<'value, 'error>`. Use it when each bound step depends on earlier successful values. For independent sibling fields where you want maximum error accumulation, prefer `Validation.map2`, `map3`, `apply`, `collect`, or `traverseIndexed` so all branches are evaluated and all diagnostics are retained.

## Builder

- [`validate`](./p-validation--validate.md): The accumulating <code>validate { }</code> computation expression.
