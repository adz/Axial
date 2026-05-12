---
weight: 20
title: Validus Integration
description: How FsFlow fits beside Validus validation pipelines.
---


# Validus Integration

This page shows how FsFlow can fit beside `Validus` validation pipelines.

`Validus` is a strong choice when the problem is still validation, especially when you want a richer DSL,
composition, accumulation, or value-object style checks.

FsFlow can usually begin after that work is done.

`Validus` and `FsFlow.Check` fit especially well together: `Validus` can handle richer validation rules,
while `FsFlow.Check` stays available for smaller pure guards that feed directly into Result,
Validation, or Flow.

## Keep Validation Before Workflow Orchestration

The best division of labor is:

- `Validus` validates the incoming model or command
- FsFlow orchestrates the application boundary, environment, runtime, typed failure, and structured validation

That keeps the validation step reusable and keeps the runtime boundary honest.

## How They Fit Together

Common patterns:

- validate with `Validus`
- convert the final success/failure into a plain Result
- bind that Result directly inside a flow when the workflow starts
- use Check when you want a smaller pure-guard layer without the heavier validation model

## Why The Pair Works

- `Validus` owns the validation story when composition, accumulation, or richer checks matter
- `FsFlow.Check` gives you a small, readable bridge when the check is a plain guard clause, option test, null check, or string predicate
- the flow family can then own the runtime boundary without swallowing validation concerns

## Example

```fsharp
let validateCreateUser dto =
    if System.String.IsNullOrWhiteSpace dto.Name then
        Error "name required"
    else
        Ok dto

let createUser : Flow<AppEnv, string, User> =
    flow {
        let! dto = validateCreateUser incoming
        let! tenant = Flow.read _.TenantId
        return { Name = dto.Name; TenantId = tenant }
    }
```

If the validation story is already richer than `FsFlow.Check` or `FsFlow.Validation`, keep it richer.
FsFlow can receive the outcome, not fight the validation library.

## When To Prefer `FsFlow.Check`

Use `FsFlow.Check` when the checks are simple and can stay purely `Result<'value, unit>`-based:

- guard clauses
- option checks
- null checks
- string emptiness checks
- simple collection checks

Use `Validus` when you want a more expressive validation DSL or validation accumulation and that
library already fits your codebase.
