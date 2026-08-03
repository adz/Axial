---
title: Introductory Reference App
description: Checks, result {}, parsing, and refinement in one small program.
type: docs
---


The introductory app uses `Axial.Result`, `Axial.Constraint`, `Axial.Refined`, and `Axial.Parse` without Schema or
Flow.

```bash
dotnet run --project examples/Axial.ReferenceApp.Intro/Axial.ReferenceApp.Intro.fsproj --nologo
```

## Reusable checks

A check proves a fact without replacing the value. `Result.guard` keeps the original value after success:

```fsharp
let validateBadgeName =
    Constraint.minLength 3
    |> Result.guard
```

Map check failures when the application has its own error vocabulary.

## Dependent work

```fsharp
result {
    let! tier = parseTier rawTier
    let! quantity = Parse.int rawQuantity |> Result.mapError (fun _ -> QuantityNotANumber rawQuantity)
    do! (quantity >= 1 && quantity <= 6) |> Result.requireTrue (QuantityOutOfRange quantity)
    return tier, quantity
}
```

## Construct domain values

```fsharp
result {
    let! parsedId = Parse.int rawId |> Result.mapError (fun _ -> InvalidId)
    let! id = AttendeeId.create parsedId |> Result.mapError (fun _ -> InvalidId)
    let! email = Refine.nonBlankString rawEmail |> Result.mapError (fun _ -> InvalidEmail)
    return AttendeeId positiveId, ContactEmail email
}
```

The full reference app adds Schema for structured input, path-aware diagnostics, codecs, and contracts, then adds Flow
for effectful application work.
