---
weight: 10
title: Getting Started
description: Build one schema up in four stages — plain fields, refined fields, constraints, and a private model behind a checked constructor.
---

# Getting Started

A `Schema<'model>` is one declaration of a structured boundary. It names fields, gives each a value schema, runs
constraints and refinements, accumulates failures with paths, and calls your constructor only once the fields succeed.

The same declaration drives several interpreters, so parsing, JSON serialization, JSON Schema generation, and metadata
inspection stay in step with no second source of truth.

This page builds one model up in four stages. Each stage adds one idea and shows what the interpreters do with it.

```bash
dotnet add package Axial.Schema
```

```fsharp
open Axial
open Axial.Schema
open Axial.Schema.Syntax
open type Axial.Schema.Syntax
```

## 1. Plain fields

Start with primitives and nothing else:

```fsharp
type Signup =
    { Email: string
      Age: int
      Newsletter: bool }

let signupSchema =
    schema<Signup> {
        field _.Email
        field _.Age
        field _.Newsletter
        construct (fun email age newsletter ->
            { Email = email; Age = age; Newsletter = newsletter })
    }
```

`field _.Email` gives the getter and nothing more. The field's value schema comes from its type — `string` resolves
`Schema.text`, `int` resolves `Schema.int`, `bool` resolves `Schema.bool` — and the wire name is the camelCased
property name. `construct` receives the fields in declaration order; the compiler checks its argument types and its
result.

Pass a wire name explicitly when it differs from the property: `field "email_address" _.Email`.

Deriving the name from `_.Email` uses a quotation, which Fable cannot compile. Write the wire name explicitly in code
that targets both .NET and Fable JavaScript — everything else on this page is unchanged. See
[Schema Syntax](../syntax/).

### Parse

`Data` is a source-neutral input tree. The same schema reads form posts, CLI arguments, JSON, and configuration —
see [Input Sources](../input-sources/).

```fsharp
let input =
    Data.ofNameValues [
        "email", "ada@example.org"
        "age", "36"
        "newsletter", "true"
    ]

Schema.parse signupSchema input
// Ok { Email = "ada@example.org"; Age = 36; Newsletter = true }
```

`"36"` arrives as text and lands as `int`. Decoding a primitive from its serialized form is part of the field's value
schema.

No `Signup` is produced unless every field succeeds and the constructor succeeds.

### Read the failures

Independent fields are all interpreted, so one parse reports every problem rather than the first:

```fsharp
match Schema.parse signupSchema input with
| Ok signup -> save signup
| Error errors ->
    for issue in SchemaErrors.toList errors do
        printfn "%s: %s" (Path.format issue.Path) (SchemaError.render issue.Error)
```

Input missing `newsletter` and carrying `"age": "not-a-number"`:

```text
age: Expected int format.
newsletter: This value was omitted.
```

Paths come from the structure of the declaration. Application code never repeats field names, nested object names, list
indexes, or map keys alongside separate validation expressions. For nesting and collections, see
[Nested Models And Collections](../tutorials/nested-and-collections/).

### Serialize

`Axial.Schema.Json` compiles the same declaration into a JSON codec. It uses no runtime reflection, so it works under
NativeAOT, trimming, and Fable.

```bash
dotnet add package Axial.Schema.Json
```

```fsharp
open Axial.Schema.Json

let codec = Json.compile signupSchema

Json.serialize codec signup
// {"email":"ada@example.org","age":36,"newsletter":true}

Json.deserialize codec json
```

Compile the codec once and reuse it. `Json.compile` does the work up front; `serialize` and `deserialize` are the hot
path. See [JSON Codec](../json-codec/) for buffers, streams, and decode diagnostics.

### The other interpreters

The same `signupSchema` also drives:

```fsharp
JsonSchema.generate signupSchema
// {"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object",
//  "properties":{"email":{"type":"string"},"age":{"type":"integer"},
//                "newsletter":{"type":"boolean"}},
//  "required":["email","age","newsletter"]}

Inspect.model signupSchema
// finite metadata: field names, shapes, constraints — no execution
```

| Input | Interpreter | Result |
| --- | --- | --- |
| `Data` | `Schema.parse` | model or `SchemaErrors` |
| an existing typed value | `Schema.check` | the same value or `SchemaErrors` |
| schema | `Json.compile` | reusable JSON codec |
| schema | `JsonSchema.generate` | JSON Schema document |
| schema | `Inspect.model` | metadata without execution |
| versioned `Data` | `Contract.parse` | current model or `ContractError` |

`Schema.check` is for values that did not arrive as `Data` — a record literal, a database mapper's output, an import.
It runs the same field rules and calls the same constructor:

```fsharp
Schema.check signupSchema existingValue
```

`Inspect.model` is what forms, admin UIs, and documentation generators read. HTTP servers use it to publish OpenAPI —
see [HTTP Servers](../http-servers/).

Everything after this point is added to the declaration once and shows up in all of these interpreters.

## 2. Refined fields

`Signup.Email` is a `string`, so nothing stops `{ Email = ""; Age = 36; Newsletter = true }`. A refined type moves that
guarantee into the model, where it holds no matter how the value was built.

[Axial.Refined]({{< relref "/error-handling/refined/" >}}) ships the common ones. Use them as field types directly:

```fsharp
open Axial.Refined

type Registration =
    { Owner: NonBlankString
      Seats: PositiveInt
      Aliases: NonEmptyList<NonBlankString> }

let registrationSchema =
    schema<Registration> {
        field _.Owner
        field _.Seats
        field _.Aliases
        construct (fun owner seats aliases ->
            { Owner = owner; Seats = seats; Aliases = aliases })
    }
```

The fields stay bare. A refinement that takes no parameters has exactly one schema, so the field resolves it from the
type the same way `string` resolved `Schema.text`. Parameterised refinements such as `boundedString` have no single
answer and need an explicit `withSchema (RefinedSchemas.boundedString 2 80)`.

`NonEmptyList<NonBlankString>` composes: the outer refinement resolves, and so does the item.

### It flows through

A refinement carries its constraints, so the interpreters see them:

```fsharp
JsonSchema.generate registrationSchema
// "owner":   {"type":"string"}
// "seats":   {"type":"integer","exclusiveMinimum":0}
// "aliases": {"type":"array","items":{"type":"string"},"minItems":1}
```

```fsharp
Inspect.model registrationSchema
// owner   -> [ "present" ]
// seats   -> [ "greaterThan" ]
// aliases -> [ "minLength" ]
```

Failures arrive on the right paths:

```text
owner: This value must be present.
seats: Must be greater than 0; got 0.
aliases: Length must be at least 1; got 0.
```

Nothing in the schema declares any of this. `PositiveInt` means "greater than zero" wherever it appears, and the
generated JSON Schema, the inspection metadata, and the parse diagnostics all read that one definition. Each check runs
once, at the layer that owns it.

Your own domain types participate the same way. See [Refined Schemas](../refined-values/) for defining `Email` or
`WorkspaceName` and contributing a canonical schema.

## 3. Constraints on fields

A refinement holds for every value of its type. A constraint holds at one boundary. Use a constraint when the rule
belongs to this form rather than to the domain type:

```fsharp
type Profile =
    { DisplayName: string
      Age: int }

let profileSchema =
    schema<Profile> {
        field _.DisplayName {
            constraints [ present; maxLength 40 ]
        }

        field _.Age {
            constrain (between 13 120)
        }

        construct (fun displayName age -> { DisplayName = displayName; Age = age })
    }
```

A field block is the expanded form of `field _.DisplayName`. `constrain` adds one; `constraints` adds a list.

Constraints reach the interpreters just as refinements do:

```fsharp
JsonSchema.generate profileSchema
// "displayName": {"type":"string","maxLength":40}
// "age":         {"type":"integer","minimum":13,"maximum":120}
```

```text
displayName: This value must be present.
age: Must be between 13 and 120; got 9.
```

A constraint preserves the value's type — `maxLength 40` on a `string` field leaves a `string` field. That is the
difference from `refine`, which changes the type and is what makes the guarantee durable.

Which to reach for:

- The rule is true of every value of the type — put it in the refinement, and every construction path enforces it.
- The rule is true only at this boundary — put it in the field block, where a reader can see it applies here.

Schema will not take metadata without an executable check behind it, so what an inspector reports is always what
parsing enforces. Constraint names come from
[Check constraints]({{< relref "/error-handling/check/constraints/" >}}); see
[Refined Schemas](../refined-values/) for application-defined constraints via `fromCheck`.

## 4. A private model behind a checked constructor

Stages 2 and 3 cover rules about one field. A rule *between* fields — a booking's start must not follow its end — has
nowhere field-local to live, and no field type can carry it.

Make the representation private so the only way to build the type runs the rule:

```fsharp
type Booking =
    private
        { Guest: NonBlankString
          Start: DateOnly
          End: DateOnly }
```

Now `{ Guest = g; Start = s; End = e }` will not compile outside the defining module, and `constructResult` becomes the
one entrance:

```fsharp
module Booking =
    let create (draft: BookingDraft) =
        if draft.Start <= draft.End then
            Ok { Guest = draft.Guest; Start = draft.Start; End = draft.End }
        else
            Error "Start must not be after end."

    let guest (booking: Booking) = booking.Guest
    let start (booking: Booking) = booking.Start
    let finish (booking: Booking) = booking.End

    let schema =
        schema<Booking> {
            field "guest" guest
            field "start" start
            field "end" finish
            constructResult (fun guest start finish ->
                create { Guest = guest; Start = start; End = finish })
        }
```

`construct` becomes `constructResult`, which returns `Result` and can reject. Fields use accessor functions because
`_.Start` needs the representation.

A reversed range now fails at the model, not at a field, so the diagnostic carries no field path:

```text
: Start must not be after end.
```

### The draft

A private record costs record syntax: callers lose `{ Guest = g; ... }` and `{ booking with End = e }`, and a positional
`create guest start finish` loses the names that make call sites readable.

A draft is a public record that exists to be assembled and edited freely, with `create` as the one way across:

```fsharp
type BookingDraft =
    { Guest: NonBlankString
      Start: DateOnly
      End: DateOnly }

module Booking =
    let toDraft (booking: Booking) : BookingDraft =
        { Guest = booking.Guest; Start = booking.Start; End = booking.End }
```

Construction keeps its field names:

```fsharp
Booking.create { Guest = guest; Start = arrival; End = departure }
```

Edits drop to the draft, use ordinary `with`, and come back through the same constructor:

```fsharp
let shift days booking =
    let draft = Booking.toDraft booking
    Booking.create { draft with Start = draft.Start.AddDays days; End = draft.End.AddDays days }
```

The draft is not a hole in the guarantee. A `BookingDraft` proves nothing; only `Booking.create` and
`Schema.parse Booking.schema` produce a `Booking`, and both run the same rule. Skipping it means editing the module
that owns the representation — a visible act, rather than a quiet record literal elsewhere in the codebase.

Every gated update returns `Result`. That is the real cost of a cross-field invariant: an edit can break the
relationship, so an infallible `with` on the checked type would be exactly the bypass this stage closes.

## Where to go next

The four stages are a ladder, not a target. Take the lowest rung that prevents a real problem:

1. Plain fields — the schema is the admission decision.
2. Refined fields — field-local invariants hold everywhere, and the record stays public with `with` intact.
3. Field constraints — boundary-specific rules, visible where they apply.
4. Private model and draft — relationships between fields.

- [Construction Guarantees](../trusted-construction/) — what each rung does and does not promise.
- [Schema Syntax](../syntax/) — the full declaration vocabulary.
- [Field Blocks and Plain Functions](../field-desugaring/) — a field block read as ordinary functions over one `Schema`.
- [Refined Schemas](../refined-values/) — your own domain types as fields.
- [Tutorials](../tutorials/) — a signup form, nested models and collections, and metadata inspection.
- [Redisplay And Field Errors](../redisplay-and-field-errors/) — failed parses that keep the user's input.
- [Versioned Contracts](../contracts/) — evolving the wire format without freezing the domain model.
