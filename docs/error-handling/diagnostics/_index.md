---
weight: 30
title: Validation
type: docs
description: Accumulating sibling failures with Validation and Diagnostics.
---

# Validation

Standard `Result` composition stops at the first failure. That is appropriate when later steps depend on earlier
ones, but less useful when several independent fields can be checked together and a caller wants every failure.

`Validation<'value, 'error>` can collect errors from sibling checks. `Diagnostics<'error>` stores those errors with
paths, indexes, and names. In `validate {}`, join independent checks with `and!`.

This is also machinery used by [Schema]({{< relref "/schema/" >}}): schema input parsing produces `Diagnostics` for you. Use it directly
when values already exist and their independent failures should be collected without declaring a schema.

If a failure should stop dependent work, an ordinary `Result` may express that more directly. Async execution and
dependency management are separate concerns; `Validation` can be used with or without [Flow]({{< relref "/flow/" >}}).

## Install

Install Diagnostics on its own:

```sh
dotnet add package Axial.Diagnostics
```

## Mental Model

```text
Result -> Validation
```

`Validation` is Result-like, but its error side is a diagnostics tree. That lets sibling failures accumulate with paths, indexes, and names attached.

## Guides

- [Tutorials](./tutorials/): validate independent fields and return all sibling failures.
- [Validate Builder](./validate-builder/): accumulating validation with `validate {}` and `and!`.
- [Schema section]({{< relref "/schema/" >}}): portable model schemas, input parsing, redisplay, rules, and policies.
- [Diagnostics](./diagnostics/): structured error graphs and rendering.

## Interop

Use `Validation.fromResult` to bring an existing fail-fast result into validation, and `Validation.toResult` when a boundary expects ordinary `Result`. `Validation.fromResult` is the canonical result-to-validation bridge; Axial does not also expose `Validation.ofResult`.

`Check` and `Result` helpers are alternatives and building blocks in the same package. Convert between them when it
helps a boundary; there is no required progression from one abstraction to another.

## Reference

- [Validation API]({{< relref "/error-handling/reference/error-handling/" >}})
- [Diagnostics API]({{< relref "/error-handling/reference/diagnostics/" >}})
