---
weight: 10
title: Validus Integration
description: How Axial fits beside Validus validation pipelines.
type: docs
---



This page shows how Axial can fit beside `Validus` validation pipelines.

`Validus` is a strong choice when the problem is still validation, especially when you want a richer DSL,
composition, accumulation, or value-object style checks.

Axial can usually begin after that work is done.

`Validus` and `Axial.Check` fit especially well together: `Validus` can handle richer validation rules,
while `Axial.Check` stays available for smaller pure guards that feed directly into Result,
Validation, or Flow.

## Keep Validation Before Workflow Orchestration

The best division of labor is:

- `Validus` validates the incoming model or command
- Axial orchestrates the application boundary, environment, runtime, typed failure, and structured validation

That keeps the validation step reusable and keeps the runtime boundary honest.

## How They Fit Together

Common patterns:

- validate with `Validus`
- convert the final success/failure into a plain Result
- bind that Result directly inside a flow when the workflow starts
- use Check when you want a smaller pure-guard layer without the heavier validation model

## Why The Pair Works

- `Validus` owns the validation story when composition, accumulation, or richer checks matter
- `Axial.Check` gives you a small, readable bridge when the check is a plain guard clause, option test, null check, or string predicate
- the flow family can then own the runtime boundary without swallowing validation concerns

## Example

The Validus README uses a `PersonDto` example to show the normal shape: validate the DTO, build a
domain record, and keep the output pure. That fits Axial cleanly because the boundary can bind the
`Result` directly.

```fsharp
open Validus

type CreateUserDto =
    { FirstName: string
      LastName: string
      Email: string
      Age: int option }

type User =
    { Name: string
      Email: string
      Age: int option
      TenantId: string }

let validateCreateUser (dto: CreateUserDto) : Result<User, ValidationErrors> =
    let nameValidator = Check.String.betweenLen 3 64
    let firstNameValidator =
        ValidatorGroup(nameValidator)
            .Then(Check.String.notEquals dto.LastName)
            .Build()

    let emailValidator =
        let emailPatternValidator =
            Check.WithMessage.String.pattern @"[^@]+@[^\.]+\..+" "Please provide a valid email address"
        ValidatorGroup(Check.String.betweenLen 8 512)
            .And(emailPatternValidator)
            .Build()

    let ageValidator = Check.optional (Check.Int.between 1 120)

    validate {
        let! first = firstNameValidator "First name" dto.FirstName
        and! last = nameValidator "Last name" dto.LastName
        and! email = emailValidator "Email" dto.Email
        and! age = ageValidator "Age" dto.Age

        return
            { Name = { First = first; Last = last }
              Email = email
              Age = age }
    }

let createUser (incoming: CreateUserDto) : Flow<AppEnv, ValidationErrors, User> =
    flow {
        let! user = validateCreateUser incoming
        let! tenantId = Flow.read _.TenantId
        return
            { Name = $"{user.Name.First} {user.Name.Last}"
              Email = user.Email
              Age = user.Age
              TenantId = tenantId }
    }
```

If the validation story is already richer than `Check` ([Error Handling]({{< relref "/error-handling/" >}})) or
`Validation` ([accumulating errors]({{< relref "/validation/" >}})), keep it richer.
Axial can receive the outcome, not fight the validation library.

## When To Prefer `Axial.Check`

Use `Axial.Check` when the checks are simple and can stay purely `Result<'value, unit>`-based:

- guard clauses
- option checks
- null checks
- string emptiness checks
- simple collection checks

Use `Validus` when you want a more expressive validation DSL or validation accumulation and that
library already fits your codebase.
