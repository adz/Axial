---
weight: 50
title: Relation to Schema
description: Apply the same refinement descriptor during schema parsing, checking, and encoding.
type: docs
---


`Axial.Refined` does not depend on `Axial.Schema`. A refined type and its smart constructor can live in a domain
package with no serialization or input-model dependency.

Schema uses the same `Refinement<'raw, 'value>` descriptor when a boundary representation must become that domain
type:

```fsharp
let quantity : Schema<PositiveInt> =
    Schema.int
    |> Schema.refine (Refinement.define Refine.positiveInt _.Value)
```

The descriptor supplies both directions:

- parsing runs the fallible constructor from `int` to `PositiveInt`;
- checking and encoding inspect an existing `PositiveInt` back to `int`.

For an application type, keep the descriptor beside the type:

```fsharp
let contactEmail : Schema<ContactEmail> =
    Schema.text
    |> Schema.refine ContactEmail.refinement
```

The record-schema CE can infer the descriptor at the `refine` line:

```fsharp
let signup =
    schema<Signup> {
        field "email" _.Email {
            withSchema Schema.text
            constrain Constraint.required
            refine
            validate validateCompanyEmail
        }

        field "age" _.Age
        construct Signup.create
    }
```

Operations run from top to bottom:

1. `withSchema Schema.text` establishes the raw boundary type.
2. `constrain` adds portable rules to that raw schema.
3. `refine` resolves `Refinement<string, ContactEmail>` from the getter result type.
4. `validate` receives `ContactEmail`, because it appears after `refine`.

`validate` is executable application logic. `constrain` is schema metadata that JSON Schema and other interpreters can
inspect. `refine` changes the field's value type. Keeping those roles separate lets one field describe all three
without nesting several builders.

See the [Schema guide]({{< relref "/schema/" >}}) for record construction and input handling.
