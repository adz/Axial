---
title: Introductory Reference App
description: Checks, result {}, and refine {} in one small program.
type: docs
---


The introductory app uses only `Axial.ErrorHandling`. It demonstrates three stages without Schema or Flow.

```bash
dotnet run --project examples/Axial.ReferenceApp.Intro/Axial.ReferenceApp.Intro.fsproj --nologo
```

## Reusable checks

`Check` functions describe rules over one typed value. `Result.orError` translates their structured failure into an
application error:

```fsharp
let validateBadgeName name =
    name
    |> Check.String.minLength 3
    |> Result.orError NameTooShort
```

## Dependent fail-fast work

`result { }` stops when one step fails. The quantity check cannot run until parsing succeeds:

```fsharp
result {
    let! tier = parseTier rawTier
    let! quantity = Parse.int rawQuantity |> Result.orError (QuantityNotANumber rawQuantity)
    do! (quantity >= 1 && quantity <= 6) |> Result.requireTrue (QuantityOutOfRange quantity)
    return tier, quantity
}
```

## Constructing domain values

`refine { }` turns raw values into types that record successful construction:

```fsharp
refine {
    let! (parsedId: int) = rawId
    let! (positiveId: PositiveInt) = parsedId
    let! (email: NonBlankString) = rawEmail
    return AttendeeId positiveId, ContactEmail email
}
```

The full reference app adds Schema for structured input, complete path-aware error reports, codecs, and contracts, then
adds Flow for effectful application work.
