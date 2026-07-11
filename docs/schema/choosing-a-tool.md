---
weight: 15
title: Choosing A Tool
type: docs
description: Schema vs Input vs Rules — the three tools inside Axial.Schema.
---

# Choosing A Tool

`Axial.Schema` splits data-boundary work into three tools. Each answers one question about the same model.

| Tool | Question it answers |
| :--- | :--- |
| `Schema<'model>` | What is the model's shape, order, construction, and constraint metadata? |
| `Model.parse` / `Model.reconstruct` | Can this raw input, or this already-existing value, become a trusted model? |
| `ContextRules.apply` | Is this already-trusted model acceptable in this context? |

## Schema

A `Schema<'model>` is declarative metadata: fields, external names, ordering, value shapes, formats, and portable
constraints. It executes nothing by itself — interpreters decide what it means.

## Input

`Model.parse schema raw` is the boundary interpreter: it parses raw field values, runs constraint checks, calls the
constructor only when every argument is trusted, and returns `ParsedInput` carrying either the model or path-aware
diagnostics plus the original input for redisplay.

Use `Model.reconstruct schema model` when the value already exists (imported rows, hand-built values) and needs the
same intrinsic constraints re-checked through getters, instead of parsed from raw input. It gives the same trust
strength as `Model.parse` — including re-invoking the model's own constructor, so cross-field invariants are
re-checked too, not just per-field constraints.

## Rules

Contextual rules are plain functions evaluated over an already-trusted model — requirements that vary by
workflow, user, tenant, clock, or feature flag. Rules never construct or transform the model.

```fsharp
match ContextRules.apply approvalRules ticket with
| Ok trusted -> approve trusted
| Error diagnostics -> reject diagnostics
```

See [Rules](../rules/) for the full picture, including custom codes and scoping combinators.

## Rule Of Thumb

Model shape → `Schema`; boundary input, or an existing value that needs re-checking → `Model.parse` /
`Model.reconstruct`; workflow-dependent acceptability of an already-trusted model → `Rules`.

## Outside This Package

Two related tools live elsewhere, for a reason:

- A single value rather than a whole model → `Check<'value>` in [Error Handling]({{< relref "/error-handling/checks/" >}})
  (`Axial.ErrorHandling`). Schema's own field constraints lower to the same checks during parsing and validation, but
  reusing a bare `Check` for one value doesn't need a schema at all.
- Running any of the above inside a workflow, with environment access, composition, or per-environment switches →
  `Policy` and `Flow.verify` in [Bind Versus Policy]({{< relref "/flow/bind/" >}}#bind-versus-policy)
  (`Axial.Flow`). `ContextRules.apply` is a plain function, so it already works inside `flow {}` without `Policy` for the
  simple case; reach for `Policy` when a rule set needs to compose with parsing or refined construction as one named
  step.
