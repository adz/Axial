---
weight: 10
title: Signup Form Tutorial
description: Declare a schema, parse form input, and redisplay errors.
type: docs
---


This page shows how to declare a schema, parse form input, and redisplay boundary errors without constructing invalid
models.

This tutorial parses a signup form into a trusted model. If any field fails, no model is constructed and the form can
be redisplayed with the user's original input and per-field errors.

## Declare The Model And Schema

The schema declares each field once: external name, getter, and constraints.

```fsharp
open Axial.Schema
open Axial.Schema.Syntax

type Signup = { Email: string; Age: int }

let signupSchema =
    Schema.define<Signup>
    |> field "email" _.Email
    |> constrain emailFormat
    |> constrain (maxLength 254)
    |> field "age" _.Age
    |> constrain (atLeast 13)
    |> construct (fun email age -> { Email = email; Age = age })
```

`Schema.define<Signup>` anchors the model type so getters can use shorthand member access. The closing constructor must
match every field in declaration order, so missing or mistyped arguments fail at `construct`.

## Adapt The Raw Input

Form posts are name/value pairs:

```fsharp
let raw =
    RawInput.ofNameValues
        [ "email", "not-an-email"
          "age", "12" ]
```

## Parse

```fsharp
let parsed = Schema.parse signupSchema raw
```

`parsed` is a `RetainedParseResult<Signup, SchemaError>`. On success `parsed.Result` is `Ok signup` and every constraint
already holds. Here both fields fail, so no `Signup` exists anywhere:

```fsharp
parsed.IsValid              // false
parsed.ErrorsFor "email"    // [ SchemaError.InvalidFormat "email" ]
parsed.ErrorsFor "age"      // [ SchemaError.OutOfRange ... ]
```

## Redisplay The Form

The original input is retained on the parsed value, addressed by the same paths:

```fsharp
RawInput.redisplayPath "email" parsed.Input   // "not-an-email", exactly as typed
RawInput.redisplayPath "age" parsed.Input     // "12"
```

A form template needs only `parsed.Input` and `parsed.ErrorsFor` — there is no half-valid model to guard against.
Use `SchemaError.render` for field-level messages or `RetainedParseResult.renderErrors parsed` for a summary list.

## Use The Trusted Model

```fsharp
match parsed.Result with
| Ok signup -> register signup      // constraints already hold; no re-checking downstream
| Error _ -> renderForm parsed
```

`Signup` here is a public record, so the guarantee belongs to the successful parse result, not to the type — other
code can still write a `Signup` literal that skips the schema. That is the right trade for a boundary form model.
When a value's construction history is uncertain, `Schema.check signupSchema value` runs the same constraints over an
already assembled value; when an invariant must hold for every value of the type, use a private representation with a
smart constructor. [Construction Guarantees](../../trusted-construction/) covers the full division.

## Next

- [Nested Models And Collections](../nested-and-collections/) for models inside models.
- [Redisplay And Field Errors](../../redisplay-and-field-errors/) for the full redisplay guide.
- [Construction Guarantees](../../trusted-construction/) for which claims need a private type rather than a schema.
