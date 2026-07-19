---
weight: 80
title: "Walkthrough: Registration Desk"
description: The introductory reference app — checks, result {}, refine {}, and validate {} in one small program, and what each adds over hand-rolled code.
---

# Walkthrough: Registration Desk

[`examples/Axial.ReferenceApp.Intro`](https://github.com/adz/Axial/tree/main/examples/Axial.ReferenceApp.Intro)
is a conference registration desk built with only the `Axial.ErrorHandling` package: no schemas, no Flow, and no
Axial type in a domain signature unless you choose a refined value. It is the first tier of the reference apps —
the [schema reference app]({{< relref "/schema/reference-apps.md" >}}) continues the same domain philosophy at
structured boundaries.

```bash
dotnet run --project examples/Axial.ReferenceApp.Intro/Axial.ReferenceApp.Intro.fsproj --nologo
```

The program is four stages, each one step up in structure. Read `Program.fs` top to bottom alongside this page.

## Stage 1 — Checks: constraints without boilerplate

```fsharp
type BadgeError = | NameTooShort | NameTooLong

let validateBadgeName (name: string) : Result<string, BadgeError> =
    name
    |> Check.minLength 3
    |> Result.orError NameTooShort
    |> Result.bind (fun name -> name |> Check.maxLength 40 |> Result.orError NameTooLong)
```

What improves over hand-rolled `if name.Length < 3 then Error ...`: the constraint is a reusable named value that
already keeps the input on success, and `Result.orError` swaps in *your* error union — the signature stays plain
`Result<string, BadgeError>`. See [Checks]({{< relref "/error-handling/checks/" >}}).

## Stage 2 — `result {}`: dependent steps fail fast

Ticket parsing is three dependent steps: the tier must parse before quantity limits mean anything.

```fsharp
result {
    let! tier = parseTier rawTier
    let! quantity = Parse.int rawQuantity |> Result.orError (QuantityNotANumber rawQuantity)
    do! (quantity >= 1 && quantity <= 6) |> Result.requireTrue (QuantityOutOfRange quantity)
    return tier, quantity
}
```

The first failure stops the pipeline — the demo shows an unknown tier reported without the quantity ever being
inspected. No nested `match` staircases, and still ordinary `Result`. See
[Result Builder]({{< relref "/error-handling/result-builder/" >}}).

## Stage 3 — `refine {}`: values that carry their proof

```fsharp
type AttendeeId = AttendeeId of PositiveInt
type ContactEmail = ContactEmail of NonBlankString

let createContact (rawId: string) (rawEmail: string) : Result<Contact, RefinementError> =
    refine {
        let! parsedId = Parse.int rawId
        let! positiveId = Refine.positiveInt parsedId
        let! email = Refine.nonBlankString rawEmail
        return { Id = AttendeeId positiveId; Email = ContactEmail email }
    }
```

A `Contact` cannot exist unless every parse and refinement succeeded; downstream code takes `Contact` and checks
nothing. The catalog types (`PositiveInt`, `NonBlankString`) preserve their invariant for the value's lifetime; the
demo shows the structured `RefinementError` a zero id produces. See
[Refined]({{< relref "/error-handling/refined/" >}}).

## Stage 4 — `validate {}`: every field failure at once, with paths

Fail-fast is wrong for forms: the user should learn about the bad email and the bad quantity together.

```fsharp
validate {
    let! name = validate.name "name" { return! validateBadgeName form.Name |> Result.mapError BadName }
    and! email = validate.name "email" { ... }
    and! ticket = validate.name "ticket" { ... }
    return { ... }
}
```

`and!` runs the fields independently and accumulates failures into a `Diagnostics<FormError>` tree;
`validate.name` attaches each failure to its field. The demo renders the bad form's report with
`Diagnostics.toString`:

```text
email:
  - InvalidEmail
name:
  - BadName NameTooShort
ticket:
  - BadTicket (QuantityOutOfRange 9)
```

Note what composed: stages 1–3 were written as fail-fast building blocks with their own error types, and stage 4
lifted them unchanged into an accumulating form report. See
[Validation]({{< relref "/error-handling/validation/" >}}).

## Where this tier stops

Everything here validates values the program already holds. When input arrives as JSON, form posts, or
configuration — where you need redisplay, wire naming, metadata, codecs, or versioning — the same philosophy
continues one tier up with [Schema]({{< relref "/schema/" >}}); when workflows need dependencies, cancellation, or
resources, see [Flow]({{< relref "/flow/" >}}).
