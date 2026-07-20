---
weight: 30
title: Validation
type: docs
description: Accumulating sibling failures with Validation and Diagnostics.
---

# Validation

Standard `Result` pipelines force an ugly choice on anyone validating a form. Fail-fast composition stops at the first
error, so the user fixes the email, resubmits, and only then learns the age was also wrong. Applicative validation
fixes that but usually at a cost: verbose `mapN`-style plumbing, and errors that arrive as a flat list detached from
the field, index, or nested record they came from — useless for redisplaying a form or pointing at `contacts[1].email`.

This section closes that gap. `Validation<'value, 'error>` accumulates every sibling failure instead of stopping at
the first, and its error side is `Diagnostics<'error>` — a structured tree that keeps paths, indexes, and names
attached to each failure. The `validate {}` builder with `and!` gives you the accumulation without the applicative
ceremony.

This is machinery behind [Schema](../): schema input parsing produces `Diagnostics` for you, so most
applications consume this section's types rather than building them by hand. Come here directly when you are
accumulating failures over values you already hold without a schema.

Use this section when independent checks should all report their failures together. If one failure should stop the operation, use [Error Handling]({{< relref "/error-handling/" >}}). If the work needs async, task interop, dependencies, resources, or runtime policy, use [Flow]({{< relref "/flow/" >}}).

## Install

`Validation` and `Diagnostics` ship inside the `Axial.ErrorHandling` package — there is no separate package to add:

```sh
dotnet add package Axial.ErrorHandling
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

Pure `Check` and `Result` helpers usually live in the [Error Handling]({{< relref "/error-handling/" >}}) section. `Validation` is the next step when the user needs all sibling errors, not only the first one.

## Reference

- [Validation API]({{< relref "/reference/validation/" >}})
- [Diagnostics API]({{< relref "/reference/diagnostics/" >}})
