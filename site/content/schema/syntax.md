---
weight: 15
title: Schema Syntax
type: docs
description: Constructor-last object schema declarations with inferred fields and adjacent constraints.
---


Open `Axial.Schema.Syntax` in the module that owns object-schema declarations:

```fsharp
module SignupSchemas =
    open Axial.Schema
    open Axial.Schema.Syntax

    let schema =
        Schema.define<Signup>
        |> field "email" _.Email
        |> constrain emailFormat
        |> field "age" _.Age
        |> constrain (atLeast 13)
        |> construct (fun email age -> { Email = email; Age = age })
```

`Schema.define<Signup>` describes structure but cannot construct a `Signup`. Each `field` adds one
typed field, and `construct` closes the declaration only when the constructor's argument order and
types match what the fields declared — checked by the compiler, at any field count. There is no
arity limit: a two-field schema and a thirty-field schema close the same way.

## Constraints sit beside their field

`constrain` applies to the field directly above it, and constraints are typed against that field's
value type — a text constraint after an `int` field fails to compile on the `constrain` line.
Constraints stack:

```fsharp
|> field "name" _.Name
|> constrain (minLength 1)
|> constrain (maxLength 100)
|> constrain trimmed
```

## Inferred fields

Primitive `string`, `int`, `decimal`, `bool`, `DateOnly`, `DateTimeOffset`, and `Guid` fields are
inferred from the getter. An owned type joins the inference by declaring its canonical schema as a
static member:

```fsharp
type EmailAddress =
    private
    | EmailAddress of string

    static member Schema(_: EmailAddress) : Schema<EmailAddress> =
        Schema.convert EmailAddress (fun (EmailAddress value) -> value) Schema.text
```

The marker argument lets F# select the member by field type at compile time. `field` then infers
`EmailAddress` directly and recursively through `EmailAddress option`, `EmailAddress list`, and
`Map<string, EmailAddress>`:

```fsharp
type Signup =
    { Email: EmailAddress option }

let schema =
    Schema.define<Signup>
    |> field "email" _.Email
    |> construct (fun email -> { Email = email })
```

F# optional type extensions do not satisfy static member constraints, so a type from another library
needs a nominal wrapper or an explicit `fieldWith` schema.

## Explicit schemas: `fieldWith`

Use `fieldWith` when a field intentionally supplies a local schema instead of its type's canonical
one — or when the type has no canonical schema to infer:

```fsharp
let inviteEmail =
    Schema.option (Schema.text |> Schema.constrain Constraint.email)

let schema =
    Schema.define<Signup>
    |> fieldWith inviteEmail "email" _.Email
    |> construct (fun email -> { Email = email })
```

Typed constraints still apply to the current field:

```fsharp
fieldWith (Schema.listWith memberSchema) "members" _.Members
|> constrain (minCount 1)
```

## Bare getters: deriving the wire name

Adding `open type Axial.Schema.Syntax` (note the `open type`) overloads `field` with a bare form that
derives the wire name from the property, camelCased:

```fsharp
module ContactSchemas =
    open Axial.Schema
    open Axial.Schema.Syntax
    open type Axial.Schema.Syntax

    let schema =
        Schema.define<Contact>
        |> field _.Name          // wire name "name"
        |> constrain (minLength 1)
        |> field _.Age           // wire name "age"
        |> construct (fun name age -> { Name = name; Age = age })
```

Two rules keep this predictable:

- An explicit name is never transformed — `field "Name" _.Name` puts `Name` on the wire exactly.
- The camelCase policy applies only to derived names, and derivation happens once when the schema
  value is built (the property name is read from the getter expression; the compiled getter itself is
  used for parsing and checking, so there is no reflection on any per-value path).

The bare form is .NET-only. It requires a plain property getter (`_.Name`) and reads that getter's F#
quotation to derive the wire name. Fable cannot interpret the quotation operation used for this name
extraction. Code compiled with Fable must declare the wire name explicitly:

```fsharp
Schema.define<Contact>
|> field "name" _.Name
|> field "age" _.Age
|> construct (fun name age -> { Name = name; Age = age })
```

Computed values and tuple projections also use the explicitly named form on .NET. The named form and
the rest of the Schema authoring API compile on .NET, NativeAOT, and Fable.

## Checked constructors

When a constructor can reject a combination of otherwise-valid fields, close with `constructResult`:

```fsharp
let rangeSchema =
    Schema.define<DateRange>
    |> field "start" _.Start
    |> field "end" _.End
    |> constructResult (fun start finish ->
        if start <= finish then Ok { Start = start; End = finish }
        else Error "end must not precede start")
```

The rejection becomes a diagnostic like any field error, placed by the parser's constructor-error
path option. The [trusted construction guide](trusted-construction.md) builds on this.

## Collections and maps as standalone schemas

Lists and string-keyed maps resolve their member schema from its type when used independently:

```fsharp
Schema.list<EmailAddress>()
Schema.map<EmailAddress>()
```

Use `Schema.listWith itemSchema` or `Schema.mapWith valueSchema` for a recursive, constrained, or
otherwise local member schema. Nested constraints are explicit about their level:

```fsharp
Schema.list<string>()
|> constrainItems (minLength 1)
|> Schema.constrain (Constraint.minCount 1)

Schema.map<string>()
|> constrainValues (minLength 1)
```

For the exact relationship between `field`, `fieldWith`, adjacent constraints, and standalone value
schemas, see [How inferred fields expand](field-desugaring.md).
