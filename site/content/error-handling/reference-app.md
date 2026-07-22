---
weight: 80
title: "Walkthrough: Registration Desk"
description: The introductory reference app — checks, result {}, refine {}, and validate {} in one small program, and what each adds over hand-rolled code.
type: docs
---


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
open Axial.ErrorHandling.CheckDSL

type BadgeError = | NameTooShort | NameTooLong

let validateBadgeName (name: string) : Result<string, BadgeError> =
    name
    |> minLength 3
    |> orError NameTooShort
    |> Result.bind (fun name -> name |> maxLength 40 |> orError NameTooLong)
```

What improves over hand-rolled `if name.Length < 3 then Error ...`: the constraint is a reusable named value that
already keeps the input on success, and `orError` swaps in your error union. The signature stays plain
`Result<string, BadgeError>`. See [Checks]({{< relref "/error-handling/checks/" >}}).

## Stage 2 — `result {}`: dependent steps fail fast

Ticket parsing is three dependent steps: the tier must parse before quantity limits mean anything.

`let!` binds the value inside `Ok` to the name on its left. `do!` binds a step whose successful value is `unit`.
`return!` would use another `Result` as the result of the block.

```fsharp
result {
    let! tier = parseTier rawTier
    let! quantity = Parse.int rawQuantity |> orError (QuantityNotANumber rawQuantity)
    do! (quantity >= 1 && quantity <= 6) |> Result.requireTrue (QuantityOutOfRange quantity)
    return tier, quantity
}
```

The first failure stops the pipeline — the demo shows an unknown tier reported without the quantity ever being
inspected. No nested `match` staircases, and still ordinary `Result`. See
[Result Builder]({{< relref "/error-handling/result-builder/" >}}).

```fsharp
result {
    let! (tier: Tier) =
        (parseTier rawTier: Result<Tier, TicketError>)

    let! (quantity: int) =
        (Parse.int rawQuantity |> orError (QuantityNotANumber rawQuantity):
            Result<int, TicketError>)

    do!
        ((quantity >= 1 && quantity <= 6)
         |> Result.requireTrue (QuantityOutOfRange quantity): Result<unit, TicketError>)
    return tier, quantity
}
// Result<Tier * int, TicketError>
```

## Stage 3 — `refine {}`: values that carry their proof

`let!` binds the parsed or refined value to the name on its left. `do!` binds a step returning `unit`.
`return!` would use another refinement result as the result of the block.

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

```fsharp
refine {
    let! (parsedId: int) = (Parse.int rawId: Result<int, ParseError>)
    let! (positiveId: PositiveInt) =
        (Refine.positiveInt parsedId: Result<PositiveInt, RefinementError>)

    let! (email: NonBlankString) =
        (Refine.nonBlankString rawEmail: Result<NonBlankString, RefinementError>)

    return { Id = AttendeeId positiveId; Email = ContactEmail email }
}
// Result<Contact, RefinementError>
```

A `Contact` cannot exist unless every parse and refinement succeeded; downstream code takes `Contact` and checks
nothing. The catalog types (`PositiveInt`, `NonBlankString`) preserve their invariant for the value's lifetime; the
demo shows the structured `RefinementError` a zero id produces. See
[Refined]({{< relref "/error-handling/refined/" >}}).

## Stage 4 — `validate {}`: every field failure at once, with paths

Fail-fast is wrong for forms: the user should learn about the bad email and the bad quantity together.

`let!` binds a successful field value to the name on its left. Sibling `and!` bindings run independently and collect
their errors.
`return!` would use another `Validation` as the result of the block.

```fsharp
validate {
    let! name = validate.name "name" { return! validateBadgeName form.Name |> mapError BadName }
    and! email = validate.name "email" { ... }
    and! ticket = validate.name "ticket" { ... }
    return { ... }
}
```

```fsharp
validate {
    let! (name: string) =
        (validateBadgeName form.Name |> mapError BadName:
            Result<string, FormError>)

    and! (email: string) =
        (validateEmail form.Email: Validation<string, FormError>)

    return { ... }
}
// Validation<Registration, FormError>
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
[Validation]({{< relref "/error-handling/diagnostics/" >}}).

## Where this tier stops

Everything here validates values the program already holds. When input arrives as JSON, form posts, or
configuration — where you need redisplay, wire naming, metadata, codecs, or versioning — the same philosophy
continues one tier up with [Schema]({{< relref "/schema/" >}}); when workflows need dependencies, cancellation, or
resources, see [Flow]({{< relref "/flow/" >}}).
