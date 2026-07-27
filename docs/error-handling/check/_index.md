---
weight: 20
title: Check
type: docs
notoc: true
description: Reusable value checks and portable typed constraints.
---

# Check

`Check<'value>` proves a fact about one typed value:

```fsharp
type Check<'value> = 'value -> Result<unit, CheckFailure list>
```

A check cannot normalize or replace its input. Use `Result.guard` when a Result pipeline should continue with the
original value after success.

```fsharp
let checkedName =
    "Ada"
    |> Result.guard Check.String.present
```

`Constraint<'value>` couples a check with portable `ConstraintDetails` metadata.

The same value facts appear at four levels:

- [Predicate](./predicates/) represents the same kinds of checks as functions returning `bool` for local branching.
- [Constraints](./constraints/) attach portable metadata to executable checks.
- [Refined values](../refined/domain-values/) use constraints to construct invariant-carrying domain types.
- [Schema]({{< relref "/schema/refined-values/" >}}) adds structured input, paths, accumulation, reconstruction, and wire interpreters.

Continue to [Using Check](./overview/) for composition and failures, [Constraints](./constraints/) for portable
metadata, or the [Check DSL](./check-dsl/) for concise definitions and Result adapters.
