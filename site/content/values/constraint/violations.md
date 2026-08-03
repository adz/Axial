---
weight: 35
title: Working with violations
description: Render, retain, map, and inspect constraint failures.
type: docs
---


`Constraint.check` returns `Error violation` when a value fails. A `Violation` is structured diagnostic data; render
it for a person, or keep it structured when application code needs to classify or translate the failure.

## Render an English message

Use `Violation.render` at the edge where the failure becomes text:

```fsharp
open Axial.Constraint

let retryCount : Constraint<int> =
    Constraint.between 0 10

33
|> Constraint.check retryCount
|> Result.mapError Violation.render
// Error "expected a value between 0 and 10, but was 33"
```

When handling the result directly:

```fsharp
match 33 |> Constraint.check retryCount with
| Ok () -> printfn "valid"
| Error violation -> printfn "%s" (Violation.render violation)
```

`render` returns an English sentence fragment with no trailing punctuation. Add punctuation or a field label in the
presentation layer that owns the complete message.

## Keep the violation in application errors

Do not turn every constraint failure into a string immediately. An application error can retain the `Violation` and
choose how to present it later:

```fsharp
type SignupError =
    | InvalidName of Violation

let name : Constraint<string> =
    Constraint.all [ Constraint.present; Constraint.lengthBetween 2 40 ]

let validateName value =
    value
    |> Constraint.guard name
    |> Result.mapError InvalidName
```

Render at the UI, log, or HTTP boundary:

```fsharp
let describe error =
    match error with
    | InvalidName violation -> $"Invalid name: {Violation.render violation}."
```

This preserves the diagnostic for comparison, testing, localization, and other projections. `Violation` is not an
application error union; wrap it in the case that identifies the failed application operation or field.

## Multiple failures preserve their meaning

`Constraint.all` reports every failed child. `Violation.render` separates those failures with `; `:

```fsharp
""
|> Constraint.check name
|> Result.mapError Violation.render
// Error "value must be present; expected a size between 2 and 40, but was 0"
```

`Constraint.any` reports the rejected alternatives only when every alternative fails. Rendering separates alternatives
with `, or `:

```fsharp
let ttl : Constraint<int> =
    Constraint.any (Constraint.equalTo -1) [ Constraint.positive ]

0
|> Constraint.check ttl
|> Result.mapError Violation.render
// Error "expected -1, but was 0, or expected a value greater than 0, but was 0"
```

The separators reflect the constraint tree: `all` means every reported condition was required, while `any` means the
value failed every permitted alternative.

## Inspect a violation without parsing its message

Built-in failures carry the failing constraint atom and, when portable, the actual value. Use the projections when
code needs those facts:

```fsharp
match "ab" |> Constraint.check (Constraint.minLength 3: Constraint<string>) with
| Ok () -> ()
| Error violation ->
    let expectation = Violation.tryExpectation violation
    // Some (CardinalityAtom (Cardinality.Minimum 3))

    let actual = Violation.tryActual violation
    // Some (ConstraintValue.Integer 2L)
```

The projections return `None` for a grouped violation because a group has more than one answer. Use
`Violation.children` for its immediate children, or `Violation.flatten` for every atomic failure in report order.
Opaque rules created with `Constraint.custom` carry author-supplied prose instead; read a single opaque leaf with
`Violation.tryDescription`.

Axial-produced groups are never empty or wrapped around one child. If only one child fails, that violation is returned
directly.

## Translate messages

`Violation.render` is the zero-dependency English default, not the only option. A `Renderer` carries the language
and the document context; the violation carries the facts:

```fsharp
let signup = renderer |> Renderer.context "signup"

violation |> Violation.message (signup |> Renderer.attribute "name")
// "must be present"

violation |> Violation.fullMessage (signup |> Renderer.attribute "name")
// "Name must be present"
```

`message` renders a bare predicate, for a form row whose label already names the field. `fullMessage` composes the
attribute noun once around the whole message, for payloads and logs. `Renderer.english` needs no resources at all.

`Violation.toMessageTree` remains available for a localization system that must control word order across a whole
group. See [Localization](./localization/) for the complete key catalogue, contextual fallback, plurals, and
advanced resolvers, and [Adding a language](./adding-a-language/) for generating a new translation.
