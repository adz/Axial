---
weight: 25
title: Field Blocks and Plain Functions
description: Read a field block as ordinary transformations over one Schema value.
---

# Field Blocks and Plain Functions

The inner field block is syntax for transforming one `Schema<_>` value. It prevents configuration for adjacent fields
from joining into one pipeline.

```fsharp
field "email" _.Email {
    withSchema Schema.text
    constrain Constraint.required
    refine
    validate validateCompanyEmail
}
```

Its schema transformation is:

```fsharp
Schema.text
|> Schema.constrain Constraint.required
|> Schema.refine ContactEmail.refinement
|> Schema.validate validateCompanyEmail
```

The outer declaration then attaches that `Schema<ContactEmail>` to the `"email"` field and its getter.

## `withSchema`

`withSchema` replaces canonical type resolution for this field:

```fsharp
field "children" _.Children {
    withSchema (Schema.listWith childSchema)
}
```

There is no separate `fieldWith` declaration. Explicit schema selection is always an operation inside the field.

## `constrain`

Portable constraints can be inspected by JSON Schema, documentation, and UI interpreters:

```fsharp
field "name" _.Name {
    constrain Constraint.required
    constrain (Constraint.maxLength 80)
}
```

The plain function is `Schema.constrain`.

## `refine`

The plain function receives a descriptor explicitly:

```fsharp
let contactEmailSchema =
    Schema.text
    |> Schema.refine ContactEmail.refinement
```

Inside the field block, the raw schema and getter supply the two types, so `refine` resolves the contributed descriptor:

```fsharp
field "email" _.Email {
    withSchema Schema.text
    refine
}
```

## `validate`

Executable validation preserves the current type:

```fsharp
let companyEmailSchema =
    contactEmailSchema
    |> Schema.validate validateCompanyEmail
```

Inside the block:

```fsharp
field "email" _.Email {
    validate validateCompanyEmail
}
```

Schema attaches the field path if the function fails. Arbitrary executable validation is not emitted as JSON Schema
metadata; use a portable constraint for facts other interpreters must read.
