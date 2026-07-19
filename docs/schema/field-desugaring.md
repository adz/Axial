---
weight: 25
title: How Inferred Fields Expand
type: docs
description: The relationship between field, fieldWith, adjacent constraints, and standalone value schemas.
---

# How inferred fields expand

Write object schemas with `field` and keep constraints beside the field:

```fsharp
let signupSchema =
    Schema.define<Signup>
    |> field "email" _.Email
    |> constrain emailFormat
    |> field "age" _.Age
    |> constrain (atLeast 13)
    |> construct Signup.create
```

That is the authoring form to prefer. The rest of this page explains how it relates to the lower-level value-schema
operations, which is useful when defining a canonical custom schema or a deliberate field-specific override.

## `field` resolves a value schema

Conceptually, this:

```fsharp
|> field "age" _.Age
```

expands to:

```fsharp
|> fieldWith (SchemaDefaults.Resolve() : Schema<int>) "age" _.Age
```

The actual call infers `int` from the getter. `SchemaDefaults.Resolve` knows Axial's built-in types and calls an
intrinsic `static member Schema` on a participating owned type. Resolution continues recursively through `option`,
`list`, and `Map<string, _>`.

The bare-getter form (`open type Axial.Schema.Syntax`, then `field _.Age`) expands one step earlier: it derives the
wire name `"age"` from the property name once, when the schema value is built, and then behaves exactly like
`field "age" _.Age`. Nothing about resolution or parsing differs.

For example, one canonical custom value schema is enough for every structural use:

```fsharp
type EmailAddress =
    private
    | EmailAddress of string

    static member Schema(_: EmailAddress) : Schema<EmailAddress> =
        Schema.text
        |> Schema.constrain Constraint.email
        |> Schema.convert EmailAddress (fun (EmailAddress value) -> value)
        |> Schema.withFormat SchemaFormat.email
```

These fields then need no explicit schema:

```fsharp
type Contacts =
    { Primary: EmailAddress
      Backup: EmailAddress option
      Team: EmailAddress list
      Aliases: Map<string, EmailAddress> }

let contactsSchema =
    Schema.define<Contacts>
    |> field "primary" _.Primary
    |> field "backup" _.Backup
    |> field "team" _.Team
    |> field "aliases" _.Aliases
    |> construct (fun primary backup team aliases ->
        { Primary = primary; Backup = backup; Team = team; Aliases = aliases })
```

## An adjacent constraint decorates the current field

For a non-optional field, these declarations have the same parsing, checking, inspection, JSON Schema, and codec
meaning:

```fsharp
Schema.define<Signup>
|> field "email" _.Email
|> constrain emailFormat
|> construct Signup.create
```

```fsharp
Schema.define<Signup>
|> fieldWith (Schema.text |> Schema.constrain Constraint.email) "email" _.Email
|> construct Signup.create
```

The first form communicates the object shape directly: infer the field schema, then constrain that field. The second
form builds a standalone `Schema<string>` first and supplies it explicitly. Prefer the first form in object schemas;
use the second idea while building reusable or canonical value schemas.

The typed `Axial.Schema.Syntax` constraints are field-authoring wrappers over the corresponding value-schema
constraints:

| Adjacent field form | Standalone schema form |
|---|---|
| `constrain emailFormat` | `Schema.constrain Constraint.email` |
| `constrain (minLength 1)` | `Schema.constrain (Constraint.minLength 1)` |
| `constrain (between 13 120)` | `Schema.constrain (Constraint.between 13 120)` |
| `constrain (maxCount 5)` | `Schema.constrain (Constraint.maxCount 5)` |

## When `fieldWith` is intentional

Use `fieldWith` when the supplied schema is part of the field's meaning:

- a third-party type cannot declare the intrinsic static member;
- one field needs a local representation or stricter policy than the type's canonical schema;
- a refined type lives in a lower-level package that cannot depend on `Axial.Schema`;
- a recursive field needs `Schema.defer`;
- an optional field constrains the inner value rather than the option itself;
- schema-level decorations such as a local default or description belong to that field schema.

Optional inner constraints are the important type distinction:

```fsharp
let optionalEmail =
    Schema.option (Schema.text |> Schema.constrain Constraint.email)

Schema.define<Invite>
|> fieldWith optionalEmail "email" _.Email
|> construct Invite.create
```

Here the email constraint has type `Constraint<string>` and applies only when a string is present. It is not a
`Constraint<string option>`, so it cannot be moved to an adjacent `constrain` call on the optional field.

## Constraints at collection levels

An adjacent collection constraint applies to the collection itself:

```fsharp
Schema.define<Team>
|> field "members" _.Members
|> constrain (minCount 1)
|> construct Team.create
```

An item or map-value constraint changes the nested schema and therefore uses the standalone combinators:

```fsharp
Schema.list<string>()
|> constrainItems (minLength 1)
|> Schema.constrain (Constraint.minCount 1)

Schema.map<string>()
|> constrainValues (minLength 1)
```

The level is always explicit: `constrain` targets the current object field, `Schema.constrain` targets a completed
schema value, and `constrainItems`/`constrainValues` target nested members.

## Prefer an intrinsic item type for an intrinsic rule

Nested constraints are unnecessary when the item type already owns the rule. Because `EmailAddress.Schema` validates
email syntax, `Schema.list<EmailAddress>()` and an inferred `EmailAddress list` field automatically validate every
item. The same is true for `EmailAddress option` and `Map<string, EmailAddress>`.

Choose the location according to what the rule means:

- Put the rule in `static member Schema` when it is universally part of the type, such as the syntax of every
  `EmailAddress`.
- Use `constrainItems` or `constrainValues` when the member type is intentionally general but this collection has a
  local policy, such as non-empty strings in one particular list.
- Use `fieldWith` when one field needs a locally configured inner schema, such as an optional raw string interpreted
  as an email only at this boundary.
- Introduce a nominal item type such as `NonBlankTag` when the same nested rule recurs because it represents a real
  domain concept.

These two declarations deliberately communicate different models:

```fsharp
Schema.list<string>()
|> constrainItems (minLength 1)  // local policy on otherwise general strings

Schema.list<NonBlankTag>()       // non-blankness is part of what a tag is
```

Moving a local rule into a canonical type schema reduces repetition and makes recursive inference work, but it also
makes that rule universal. Do that only when callers should never need a different schema for the same nominal type.
