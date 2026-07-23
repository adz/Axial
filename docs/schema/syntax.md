---
weight: 20
title: Schema Syntax
description: Record fields, field blocks, canonical schemas, and checked constructors.
---

# Schema Syntax

A record schema is one constructor-last computation expression:

```fsharp
schema<Signup> {
    field "email" _.Email
    field "age" _.Age
    construct Signup.create
}
```

`field` and `construct` use implicit yield. Do not write `yield`.

## Fields without blocks

The getter fixes the field type. Schema resolves that type's canonical schema:

```fsharp
field "name" _.Name
field "age" _.Age
field "tags" _.Tags
```

This works for built-in primitives and composites, built-in refined values, and application types that contribute a
static `Schema` member.

On .NET, a quotation can supply the default wire name:

```fsharp
field _.Name
```

Use the explicit form in portable code:

```fsharp
field "name" _.Name
```

Fable cannot perform the quotation operation that derives the member name. Explicit wire names compile on .NET and
Fable.

## Field blocks

A block groups transformations for one field:

```fsharp
field "email" _.Email {
    withSchema Schema.text
    constrain Constraint.required
    refine
    validate validateCompanyEmail
}
```

Operations run from top to bottom:

1. `withSchema` sets the current raw schema.
2. `constrain` adds portable metadata and an executable check without changing the value type.
3. `refine` changes the current schema from its raw type to the getter type.
4. `validate` runs executable value-preserving logic over the current type.

The block must finish with the getter type. A plain `int` field does not need refinement:

```fsharp
field "age" _.Age {
    withSchema Schema.int
    constrain (Constraint.atLeast 18)
}
```

## Refinement changes the stage

```fsharp
field "email" _.Email {
    withSchema Schema.text
    constrain Constraint.required       // operates on string
    refine                              // string -> ContactEmail
    validate validateCompanyEmail       // operates on ContactEmail
}
```

The parameterless operation resolves `Refinement<string,ContactEmail>` at compile time. A missing contribution is a
compile error; Schema does not use reflection or a runtime registry.

## Constructors

`construct` accepts a total constructor:

```fsharp
construct (fun email age -> { Email = email; Age = age })
```

`constructResult` accepts cross-field construction that can fail:

```fsharp
constructResult Signup.createChecked
```

All independent fields must succeed before either constructor runs. A `constructResult` failure attaches to the current
object path.

The field chain is recursive and has no fixed arity limit.

## Recursive schemas

Use `Schema.defer` where a field refers back to the schema being defined:

```fsharp
let rec schema : Lazy<Schema<Category>> =
    lazy (
        SchemaCE.schema<Category> {
            field "name" _.Name
            field "children" _.Children {
                withSchema (Schema.listWith (Schema.defer schema))
            }
            construct Category.create
        })
```

Only the opening builder is qualified here because the binding named `schema` shadows the unqualified builder.
Ordinary declarations use unqualified `schema`, `field`, and `construct`.
