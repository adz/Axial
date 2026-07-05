---
weight: 25
title: Redisplay And Field Errors
type: docs
description: Failed parses that keep the user's input.
---

# Redisplay And Field Errors

When boundary input fails to parse, a form should show the user's original text next to each field's errors. Axial's
`ParsedInput` keeps both: the raw input exactly as submitted, and diagnostics addressed by path.

## The Handoff Value

`Input.parse` always returns a `ParsedInput<'model, SchemaError>`:

```fsharp
let parsed = Input.parse customerSchema raw

parsed.IsValid        // true when a trusted model exists
parsed.Result         // Ok model | Error diagnostics
parsed.Input          // the original RawInput, always retained
parsed.Errors         // flattened path-aware errors ([] when valid)
```

## Field Error Lookup

`ErrorsFor` addresses errors with the same path text used by raw input, including collection indexes:

```fsharp
parsed.ErrorsFor "email"                // errors attached exactly to the email field
parsed.ErrorsFor "contacts[1].value"    // errors on the second contact's value
```

`SchemaError` deliberately omits the field name — the diagnostics path already carries it — so the same error value
renders correctly wherever it is attached.

## Redisplay

`RawInput` addresses submitted values by the same paths:

```fsharp
RawInput.redisplayPath "email" parsed.Input          // "not-an-email", exactly as typed
RawInput.redisplayPath "contacts[1].value" parsed.Input
```

Missing input redisplays as blank text, so form templates never special-case absent fields.

## Rendering A Form

The typical loop over a failed parse:

```fsharp
for field in formFields do
    let value = RawInput.redisplayPath field.Path parsed.Input
    let errors = parsed.ErrorsFor field.Path
    render field value errors
```

Because failed parses never construct the model, there is no half-valid object to guard against — the template works
from raw input and diagnostics only.

## Mapping To Domain Errors

`ParsedInput.mapErrors` translates interpreter errors into a domain or application error type at the boundary while
preserving the raw input and paths:

```fsharp
let domainParsed = parsed |> ParsedInput.mapErrors SignupError.ofSchemaError
```
