---
weight: 20
title: Schema Syntax
type: docs
description: Constructor-last object schema declarations with inferred fields and adjacent constraints.
---

# Schema Syntax

Open `Axial.Schema.Syntax` in the module that owns object-schema declarations:

```fsharp
module SignupSchemas =
    open Axial.Schema
    open Axial.Schema.Syntax

    let schema =
        Schema.define<Signup>
        |> field "email" _.Email
        |> constrain email
        |> field "age" _.Age
        |> constrain (atLeast 13)
        |> construct (fun email age -> { Email = email; Age = age })
```

`Schema.define<Signup>` describes structure but cannot construct a `Signup`. Each `field` grows the shape's
compile-time field list. `construct` closes the shape only when its argument order and types match.

Use `fieldWith` when the value schema cannot be inferred:

```fsharp
let schema =
    Schema.define<Signup>
    |> fieldWith Email.schema "email" _.Email
    |> fieldWith (Schema.listWith Tag.schema) "tags" _.Tags
    |> construct (fun email tags -> { Email = email; Tags = tags })
```

Typed constraints apply to the current field, so invalid placements fail where they are written:

```fsharp
fieldWith (Schema.listWith memberSchema) "members" _.Members
|> constrain (minCount 1)
```

Primitive `string`, `int`, `decimal`, `bool`, `DateOnly`, `DateTimeOffset`, and `Guid` fields—and their common option
and list forms—are inferred. Other types require `fieldWith` or a `DefaultSchema` static member.

Lists and string-keyed maps also resolve their member schema from its type when used independently:

```fsharp
Schema.list<Email>()
Schema.map<Email>()
```

Use `Schema.listWith itemSchema` or `Schema.mapWith valueSchema` for a recursive, constrained, or otherwise local
member schema. Nested constraints remain explicit about their level:

```fsharp
Schema.list<string>()
|> constrainItems (minLength 1)
|> Schema.constrain (Constraint.minCount 1)

Schema.map<string>()
|> constrainValues (minLength 1)
```
