
This page shows which correctness claims Schema can enforce and which claims require an invariant-preserving F# type.

## The limit

A library can guarantee the result of its own functions. It cannot prevent a caller from using another public
constructor:

```fsharp
type Booking =
    { Start: DateOnly
      End: DateOnly }

let bookingSchema =
    Schema.recordFor<Booking, _> (fun start finish ->
        if start <= finish then Ok { Start = start; End = finish }
        else Error "Start must not be after end.")
    |> Schema.field "start" _.Start Schema.date
    |> Schema.field "end" _.End Schema.date
    |> Schema.buildResult

let invalidAnyway =
    { Start = DateOnly(2026, 7, 20)
      End = DateOnly(2026, 7, 12) }
```

`Schema.parse bookingSchema raw` and `Schema.check bookingSchema value` enforce the constructor rule. The public record
literal does not. There is no honest library-level type guarantee for every `Booking` while that literal remains
available.

## Durable field invariants

Private refined fields make illegal field values unrepresentable:

```fsharp
type WorkspaceName = private WorkspaceName of NonBlankString

module WorkspaceName =
    let create raw =
        Refine.nonBlankString raw |> Result.map WorkspaceName

    let value (WorkspaceName name) = name.Value

    let schema : Schema<WorkspaceName> =
        Schema.text
        |> Schema.constrainAll [ Constraint.required; Constraint.maxLength 80 ]
        |> Schema.refine create SchemaError.ofRefinementError value
```

`Schema.refine` takes the real fallible smart constructor. Raw constraints can report common failures with familiar
codes and metadata; the smart constructor remains authoritative. If the two declarations drift, refinement returns
diagnostics instead of throwing.

Any `WorkspaceName` is valid because its representation is private and every exposed constructor returns `Result`.
The schema participates in that construction; it is not the sole guardian.

## Durable aggregate invariants

Use a private aggregate representation when every value must satisfy a relationship between fields:

```fsharp
type Booking =
    private
        { Start: DateOnly
          End: DateOnly }

module Booking =
    let create start finish =
        if start <= finish then Ok { Start = start; End = finish }
        else Error "Start must not be after end."

    let start booking = booking.Start
    let finish booking = booking.End

    let schema =
        Schema.recordFor<Booking, _> create
        |> Schema.field "start" start Schema.date
        |> Schema.field "end" finish Schema.date
        |> Schema.buildResult
```

The record schema invokes `Booking.create`, and ordinary callers cannot use a record literal. Updates must also go
through functions that preserve the rule. A private constructor with a public unchecked `with`-style escape hatch is
not complete encapsulation.

## Drafts and contracts

Wire records often should remain easy to construct:

```fsharp
type BookingV1 =
    { start: DateOnly
      ``end``: DateOnly }
```

Their job is representation, not durable domain correctness. Parse them through a schema and map once into the domain
type. Naming the value `draft`, `wire`, or `contract` makes the trust boundary visible. Do not pass it through business
logic as though its schema had changed the record's constructors.

## Existing typed values

`Schema.check` is for values whose construction history is uncertain:

```fsharp
match Schema.check bookingSchema importedBooking with
| Ok checked -> useBooking checked
| Error diagnostics -> quarantine diagnostics
```

It recursively checks fields and collections and re-invokes a record schema's constructor. It returns the original
value on success; it does not create a proof wrapper. The guarantee belongs to that successful result flow. A private
domain representation carries a stronger, durable guarantee.

## Recommendation

The reference app uses all three levels deliberately:

- `WorkspaceV1` and `WorkspaceV2` are public wire records.
- `WorkspaceName`, `PersonName`, and `WorkItemTitle` have private representations and fallible constructors.
- `Workspace` business transitions accept only refined fields and return `Result` for relational business rules.
- persisted contracts are parsed again when read; contextual production rules run only at the relevant admission
  boundary.

This division keeps schema metadata useful without claiming that metadata overrides F# construction semantics.
