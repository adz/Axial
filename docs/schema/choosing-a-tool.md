---
weight: 15
title: Choosing A Tool
type: docs
description: Schema vs Input vs Check vs Rules vs Policy.
---

# Choosing A Tool

Axial splits data-boundary work into five small tools. Each answers one question.

| Tool | Question it answers | Package |
| :--- | :--- | :--- |
| `Check<'value>` | Does this single value satisfy a reusable constraint? | `Axial.ErrorHandling` |
| `Schema<'model>` | What is the model's shape, order, construction, and constraint metadata? | `Axial.Schema` |
| `Input.parse` | Can this raw boundary input become a trusted model? | `Axial.Validation.Schema` |
| `Rules.apply` | Is this already-trusted model acceptable in this context? | `Axial.Validation.Schema` |
| `Policy` + `Flow.verify` | How does a verification step run inside a workflow with its environment? | `Axial.Flow` |

## Check

A `Check<'value>` is a path-free, raw-input-free executable constraint over one value. Reuse checks anywhere a plain
value needs guarding; schemas lower their constraint metadata to the same checks during parsing and validation.

```fsharp
let nameCheck = Check.all [ Check.String.present; Check.String.maxLength 80 ]
```

## Schema

A `Schema<'model>` is declarative metadata: fields, external names, ordering, value shapes, formats, and portable
constraints. It executes nothing by itself — interpreters decide what it means.

## Input

`Input.parse schema raw` is the boundary interpreter: it parses raw field values, runs constraint checks, calls the
constructor only when every argument is trusted, and returns `ParsedInput` carrying either the model or path-aware
diagnostics plus the original input for redisplay.

Use `Validation.validate schema model` (from `Axial.Validation.Schema`) when the value already exists — imported rows,
hand-built values — and needs the same intrinsic constraints re-checked through getters.

## Rules

A `RuleSet<'model, 'error>` evaluates contextual requirements over an already-trusted model — requirements that vary by
workflow, user, tenant, clock, or feature flag. Rules never construct or transform the model.

```fsharp
match Rules.apply approvalRules ticket with
| Ok trusted -> approve trusted
| Error diagnostics -> reject diagnostics
```

## Policy

A `Policy<'env, 'error, 'input, 'output>` adapts any of the above — parsers, refined constructors, schema input
results, validation results, rule sets — into a named, composable verification step that `Flow.verify` runs with the
workflow environment injected.

```fsharp
let acceptLine : Policy<OrderEnv, OrderError, RawInput, OrderLine> =
    Policy.compose parseOrderLine (Policy.optional _.EnforceQuantityCap underQuantityCap)

let workflow raw = flow {
    let! line = raw |> Flow.verify acceptLine
    return line
}
```

## Rule Of Thumb

Start from the value and move outward: reusable value constraint → `Check`; model shape → `Schema`; boundary input →
`Input.parse`; workflow acceptability → `Rules`; running any of it inside `flow { }` with an environment → `Policy`.
For one-off error assignment at a single bind site, plain [`Bind`]({{< relref "/flow/bind/" >}}) is still the lighter
tool.
