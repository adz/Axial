---
weight: 25
title: Field Blocks and Plain Functions
description: Read a field block as ordinary transformations over one Schema value.
type: docs
---


The inner field block is syntax for transforming one `Schema<_>` value. It prevents configuration for adjacent fields
from joining into one pipeline.

```fsharp
field "email" _.Email {
    withSchema Schema.text
    refine ContactEmail.refinement
    validate validateCompanyEmail
}
```

Its schema transformation is:

```fsharp
Schema.text
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
    constraints [ required; maxLength 80 ]
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

Inside the field block, either supply the same value or use the destination type's canonical contribution:

```fsharp
field "email" _.Email {
    withSchema Schema.text
    refine ContactEmail.refinement
}
```

```fsharp
type ContactEmail with
    static member Refinement(_: string, _: ContactEmail) = ContactEmail.refinement

field "email" _.Email {
    withSchema Schema.text
    refine
}
```

The bare form uses the current `string` schema and the `ContactEmail` getter type as its compile-time dispatch key.

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
