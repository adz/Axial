---
weight: 20
title: Constraints
type: docs
description: Couple executable checks to portable metadata for refined values and Schema interpreters.
---

# Constraints

A `Check<'value>` can execute a rule, but execution alone does not explain that rule to another interpreter. A
`Constraint<'value>` keeps the executable check together with stable, inspectable metadata:

```fsharp
type ConstraintDetails =
    { Code: string
      Arguments: Map<string, ConstraintArgument> }
```

Use a constraint when a value restriction should be reusable beyond one direct function call. Use a plain Check when
only executable behavior matters.

## Execute and inspect the same rule

```fsharp
open Axial.Check

let nameLength : Constraint<string> =
    Constraint.maxLength 80

let check : Check<string> =
    Constraint.check nameLength

let details : ConstraintDetails =
    Constraint.details nameLength

check "Ada"
// Ok ()

// details.Code = "maxLength"
// details.Arguments = Map [ "maximum", ConstraintArgument.Integer 80L ]
```

`Constraint.checkAll` executes several constraints against the same value and accumulates their `CheckFailure`
values:

```fsharp
let contactEmail =
    Constraint.checkAll
        [ Constraint.required
          Constraint.email
          Constraint.maxLength 254 ]
```

## Built-in constraints

The built-in set covers:

- required and optional values
- text length, email format, trimming, patterns, and closed choices
- equality and ordered bounds
- collection counts, distinctness, and containment
- numeric multiples

Metadata arguments use the closed `ConstraintArgument` union rather than `obj`, so consumers do not need reflection or
runtime type guesses.

## Define application constraints

Use `Constraint.define` when an application has a named rule that other consumers can understand:

```fsharp
let even : Constraint<int> =
    Constraint.define
        "even"
        Seq.empty
        (fun value ->
            if value % 2 = 0 then Ok ()
            else Error [ CheckFailure.Custom "even" ])
```

Custom codes belong to the application. Axial reserves its built-in codes so that their meaning remains stable.
A custom constraint is portable only to interpreters that understand its code and arguments; every consumer can still
execute its Check.

## Where constraints are used

### Direct checking

Extract the Check with `Constraint.check`, or combine several with `Constraint.checkAll`. This produces ordinary
`Result<unit, CheckFailure list>` without requiring Schema or Refined.

### Refined domain values

A [`Refinement<'underlying,'refined>`]({{< relref "/error-handling/refined/domain-values/" >}}) owns one or more
constraints. `Refinement.create` executes them before constructing the private destination value, while
`Refinement.constraints` exposes their metadata.

```fsharp
let contactEmail =
    Refinement.defineAll
        [ Constraint.required
          Constraint.email
          Constraint.maxLength 254 ]
        ContactEmail
        ContactEmail.value
```

### Structured Schema boundaries

[Schema field blocks]({{< relref "/schema/refined-values/" >}}) can apply constraints directly to a raw field or apply
a constraint-backed refinement. Schema adds the field path, accumulates failures across the model, and preserves the
metadata for inspection and applicable interpreters.

```fsharp
field "email" _.Email {
    withSchema Schema.text
    constraints [ required; maxLength 80 ]
    refine Email.refinement
}
```

The same metadata can inform [JSON Schema generation]({{< relref "/schema/reference/schema/m-schema-jsonschema-generate" >}}), forms, contract
inspection, and other Schema consumers. Keep boundary-specific restrictions in the field block; put universal domain
invariants in the refinement.

## Continue

- [Using Check](../overview/) for executable composition and failures.
- [Check DSL](../check-dsl/) for concise check definitions and Result adapters.
- [Refined Values in Schema]({{< relref "/schema/refined-values/" >}}) for the complete field progression.
