---
weight: 30
title: Redisplay And Field Errors
type: docs
description: Failed parses that keep the user's input.
---


This page shows how failed schema parses retain structured data, path-aware field errors, and default display strings.

When boundary input fails to parse, a form should show the user's original text next to each field's errors. Axial's
`RetainedParseResult` keeps both: the structured data exactly as submitted, and diagnostics addressed by path.

## The Handoff Value

Use `Schema.parseRetainingInput` when the boundary needs the submitted representation after parsing:

```fsharp
let parsed = Schema.parseRetainingInput customerSchema raw

parsed.IsValid        // true when a trusted model exists
parsed.Result         // Ok model | Error diagnostics
parsed.Input          // the original Data, always retained
parsed.Errors         // flattened path-aware errors ([] when valid)
```

Schema parsing, schema validation, primitive `Parse` failures, `Refine`
failures, and value-level `Violation` values all lower to the same boundary taxonomy: `SchemaError`.

## Field Error Lookup

`ErrorsFor` addresses errors with the same path text used by structured data, including collection indexes:

```fsharp
parsed.ErrorsFor "email"                // errors attached exactly to the email field
parsed.ErrorsFor "contacts[1].value"    // errors on the second contact's value
```

`SchemaError` deliberately omits the field name — the diagnostics path already carries it — so the same error value
renders correctly wherever it is attached.

## Redisplay

`Data` addresses submitted values by the same paths:

```fsharp
Data.redisplayPath "email" parsed.Input          // "not-an-email", exactly as typed
Data.redisplayPath "contacts[1].value" parsed.Input
```

Absent input looks up as `Data.Null` and redisplays as blank text, so form templates never special-case absent fields.

## Rendering A Form

The typical loop over a failed parse:

```fsharp
for field in formFields do
    let value = Data.redisplayPath field.Path parsed.Input
    let errors = parsed.ErrorsFor field.Path |> List.map SchemaError.render
    render field value errors
```

Because failed parses never construct the model, there is no half-valid object to guard against — the template works
from structured data and diagnostics only.

For summary output, render every failed diagnostic in one line:

```fsharp
let messages = RetainedParseResult.renderErrors parsed
// [ "email: Expected email format."; "age: Must satisfy atLeast 13; got 12." ]
```

## Localized Messages

`SchemaError.render` is the zero-dependency English default. To render in a language, pass a `Renderer` and let
Schema fold its typed path in as the attribute:

```fsharp
open Axial.Constraint

let signup = renderer |> Renderer.context "signup"

errors |> SchemaErrors.messages signup
// [ Path "email",              "must be a valid email"
//   Path "contacts[1].value",  "must be present" ]

errors |> SchemaErrors.fullMessages signup
// [ Path "email",              "Email must be a valid email"
//   Path "contacts[1].value",  "Value must be present" ]

errors |> SchemaErrors.toStringWith signup   // one full message per line
```

`messages` returns bare predicates, which is what a form wants: the returned `Path` already identifies the field, so
a template that renders its own label does not print the name twice. `fullMessages` composes the attribute noun once
for payloads, logs, and summaries.

You supply only the document context. Index components stay out of resource keys — `contacts[0].value` and
`contacts[1].value` are one field for a translator — and stay in every returned path, so field lookup and redisplay
still work:

```fsharp
for field in formFields do
    let value = Data.redisplayPath field.Path parsed.Input
    let messages =
        errors
        |> SchemaErrors.messages signup
        |> List.filter (fun (path, _) -> Path.format path = field.Path)
        |> List.map snd

    render field value messages
```

Schema's own parse and structural failures have a `schema.*` catalogue (`SchemaMessages.keys`); constraint failures
use Axial's `constraint.*` catalogue. Both render through the same mechanics. See
[Localization]({{< relref "/error-handling/constraint/localization/" >}}) for the key catalogue and
[Adding a language]({{< relref "/error-handling/constraint/adding-a-language/" >}}) for generating a translation.

## Mapping To Domain Errors

`RetainedParseResult.mapErrors` translates interpreter errors into a domain or application error type at the boundary while
preserving the structured data and paths:

```fsharp
let domainParsed = parsed |> RetainedParseResult.mapErrors SignupError.ofSchemaError
```

That mapping is the boundary between Axial's interpreter errors and your application errors. Keep your user-owned error
union in the application, and translate `SchemaError` with one function when the parsed result crosses that boundary.
