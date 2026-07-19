---
title: Build A Private Aggregate
weight: 10
description: Keep a cross-field invariant behind one constructor while retaining a public draft for editing.
type: docs
---


Use a private aggregate when every value passed through business code must satisfy a relationship between fields.

An **aggregate** is a group of related values that the application creates and updates as one thing. An **invariant**
is a rule that must remain true for every valid instance of that thing.

A booking is a typical example. `Start <= End` is not a rule for one field. A public record lets any caller bypass the
rule with a literal or record-copy update.

## Define an editable draft

The draft is intentionally untrusted. Forms, tests, and mapping code can assemble and edit it freely.

```fsharp
namespace MyApp.Domain

open System

type BookingDraft =
    { Start: DateOnly
      End: DateOnly }

[<RequireQualifiedAccess>]
type BookingError =
    | EndBeforeStart
```

## Hide the aggregate representation

Keep the record private and expose functions that return only valid bookings.

```fsharp
type Booking =
    private
        { Start: DateOnly
          End: DateOnly }

[<RequireQualifiedAccess>]
module Booking =

    let create (draft: BookingDraft) =
        if draft.Start <= draft.End then
            Ok
                { Start = draft.Start
                  End = draft.End }
        else
            Error BookingError.EndBeforeStart

    let start booking = booking.Start
    let finish booking = booking.End

    let toDraft booking =
        { Start = booking.Start
          End = booking.End }
```

Outside this module, callers cannot construct `Booking` or use `{ booking with End = value }`.

## Build the schema through the same constructor

A **schema** is Axial's typed description of input shape, field constraints, and construction. It parses boundary fields,
then calls the authoritative constructor.

```fsharp
open Axial.Schema
open Axial.Schema.Syntax

module Booking =
    // create, accessors, and toDraft from above

    let schema : Schema<Booking> =
        Schema.define<Booking>
        |> field "start" start
        |> field "end" finish
        |> constructResult (fun start finish ->
            create { Start = start; End = finish }
            |> Result.mapError (function
                | BookingError.EndBeforeStart -> "End must not be before start."))
```

Both direct construction and boundary parsing now use `Booking.create`.

```fsharp
match (Schema.parse Booking.schema raw) with
| Ok booking -> save booking
| Error diagnostics -> display diagnostics
```

## Use an F# signature file for a hard interface

For an important domain module, an `.fsi` file makes the representation opaque even if the implementation uses a
normal record. It also gives reviewers a short list of allowed operations.

```fsharp
// Booking.fsi
namespace MyApp.Domain

open System
open Axial.Schema

type BookingDraft =
    { Start: DateOnly
      End: DateOnly }

[<RequireQualifiedAccess>]
type BookingError =
    | EndBeforeStart

type Booking

[<RequireQualifiedAccess>]
module Booking =
    val create: BookingDraft -> Result<Booking, BookingError>
    val start: Booking -> DateOnly
    val finish: Booking -> DateOnly
    val toDraft: Booking -> BookingDraft
    val schema: Schema<Booking>
```

Place `Booking.fsi` immediately before `Booking.fs` in the project file:

```xml
<ItemGroup>
  <Compile Include="Booking.fsi" />
  <Compile Include="Booking.fs" />
</ItemGroup>
```

The compiler rejects implementation members or representations that leak through the signature.

## When not to use this pattern

Keep a public record when callers are expected to create arbitrary drafts and trust exists only after one operation.
Use refined field types when every invariant is local to a field; they preserve normal record-copy syntax.

Private aggregates pay off when many modules rely on the same cross-field fact or when an invalid update would be hard
to trace back to its source.
