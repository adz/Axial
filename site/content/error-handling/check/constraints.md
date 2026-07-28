---
weight: 20
title: Constraints
type: docs
description: Give executable checks typed metadata that other code can inspect.
---


A `Check<'value>` can execute a rule, but other code cannot inspect an F# function to learn what that rule means. A
`Constraint<'value>` keeps the check together with typed `ConstraintMetadata`.

For example, `Constraint.maxLength 80` carries `ConstraintMetadata.MaxLength 80`. Its code is the canonical external
name `"maxLength"`. Codes are derived from the metadata rather than authored separately, so a built-in constraint cannot
pair the `"email"` code with `MaxLength 80` metadata.

Use a constraint when both execution and inspection matter. Use a plain Check when only executable behavior matters.

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

// Constraint.metadata nameLength = ConstraintMetadata.MaxLength 80
// details.Code = "maxLength"
// details.Arguments = Map [ "maximum", box 80 ]
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

- required text and boundary-presence annotations used by Schema
- text length, email format, trimming, patterns, and closed choices
- equality and ordered bounds
- collection counts, distinctness, and containment
- numeric multiples

`Required` and `Optional` are the two presence cases in the shared metadata vocabulary. Schema uses them before a typed
value exists, so its `required` and `optional` descriptors intentionally have no retained value check. All other Schema
constraints retain and execute a complete `Constraint<'value>`.

The discriminated-union cases identify each rule without string dispatch. Operands of generic rules are retained as
`obj`, preserving application types at runtime; consumers inspect the case before interpreting those operands. When
metadata must cross a serialization boundary, `Constraint.tryPortableArguments` projects supported operands to the closed
`ConstraintArgument` union. It returns `None` rather than converting an unsupported value to a lossy string.

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

Custom codes belong to the application. Axial reserves its built-in codes so their meaning remains stable. A custom
constraint is always complete: it includes executable checking behavior as well as its code and arguments. An
interpreter that does not recognize the custom metadata can still execute its Check.

## Where constraints are used

### Direct checking

Extract the Check with `Constraint.check`, or combine several with `Constraint.checkAll`. This produces ordinary
`Result<unit, CheckFailure list>` without requiring Schema or Refined.

### Refined domain values

[Refined values]({{< relref "/error-handling/refined/domain-values/" >}}) use constraints to decide whether an
underlying value may be constructed as a domain value.

### Structured Schema boundaries

[Schema fields]({{< relref "/schema/refined-values/" >}}) use the same constraints while adding paths and accumulated
diagnostics. Schema interpreters can inspect the metadata without reimplementing the Check.

## Continue

- [Using Check](../overview/) for executable composition and failures.
- [Check DSL](../check-dsl/) for concise check definitions and Result adapters.
- [Refined Values in Schema]({{< relref "/schema/refined-values/" >}}) for the complete field progression.
