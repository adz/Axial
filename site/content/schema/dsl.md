---
weight: 20
title: Schema DSL
type: docs
description: The unqualified authoring view of the universal Schema catalog.
---


This page shows the unqualified authoring view of the universal Schema catalog.

Open `Axial.Schema.DSL` inside a schema-definition module when repeated qualifiers obscure the declaration:

```fsharp
module SignupSchemas =
    open Axial.Schema
    open Axial.Schema.DSL

    let schema =
        recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
        |> field "email" _.Email (text |> constrainAll [ required; email ])
        |> field "age" _.Age (int |> constrain (atLeast 13))
        |> build
```

The equivalent qualified form is:

```fsharp
let schema =
    Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
    |> Schema.field "email" _.Email
        (Schema.text |> Schema.constrainAll [ Constraint.required; Constraint.email ])
    |> Schema.field "age" _.Age
        (Schema.int |> Schema.constrain (Constraint.atLeast 13))
    |> Schema.build
```

The DSL aliases primitives, `list`, `option`, `map`, `defer`, `convert`, `refine`, unions, enums, schema decorators,
record builders, constraints, `parse`, and `check`. Each alias delegates to `Schema` or `Constraint`; there is no DSL-
specific schema type and no constraint-first field overload.

Constraints decorate schemas before those schemas become fields:

```fsharp
field "members" _.Members
    (list memberSchema |> constrain (minCount 1))
```

This keeps the same composition rule for a root schema, a nested field, a list item, or a union payload.

Names including `int`, `decimal`, and `bool` shadow FSharp.Core conversion functions. Limit the `open` to the module
that owns declarations. Qualified `Schema.*` and `Constraint.*` calls are clearer in general application code.
