---
weight: 30
title: Validation
type: docs
description: Accumulating sibling failures with Validation and Diagnostics.
---

# Validation

Use this section for accumulating sibling failures with `Validation<'value, 'error>`, `Diagnostics<'error>`, and the `validate {}` builder.

Use this section when independent checks should all report their failures together. If one failure should stop the operation, use [Error Handling](../error-handling/). If the work needs async, task interop, dependencies, resources, or runtime policy, use [Flow](../flow/).

## Mental Model

```text
Result -> Validation
```

`Validation` is Result-like, but its error side is a diagnostics tree. That lets sibling failures accumulate with paths, indexes, and names attached.

## Start Here

- [Tutorials](./tutorials/): validate independent fields and return all sibling failures.
- [Validate Builder](./validate-builder/): accumulating validation with `validate {}` and `and!`.
- [Schema Boundaries](./schema-boundaries/): choose constructor invariants or contextual rules.
- [Schema section]({{< relref "/schema/" >}}): portable model schemas, input parsing, redisplay, rules, and policies.
- [Diagnostics](./diagnostics/): structured error graphs and rendering.

## Interop

Use `Validation.fromResult` to bring an existing fail-fast result into validation, and `Validation.toResult` when a boundary expects ordinary `Result`. `Validation.fromResult` is the canonical result-to-validation bridge; Axial does not also expose `Validation.ofResult`.

Pure `Check` and `Result` helpers usually live in the [Error Handling](../error-handling/) section. `Validation` is the next step when the user needs all sibling errors, not only the first one.

## Reference

- [Validation API]({{< relref "/reference/validation/" >}})
- [Diagnostics API]({{< relref "/reference/diagnostics/" >}})
