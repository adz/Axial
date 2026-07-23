---
title: Model Legal Transitions
weight: 20
description: Keep trusted aggregates valid by exposing named updates instead of unrestricted record copies.
type: docs
---


Construction is only the first guard. A valid value can become invalid when application code updates it.

A **transition** is a named operation that changes one valid state into another, such as completing an order or moving a
booking. It may return an error when the requested change is not allowed.

Keep updates in the aggregate module and name them after the business action. Callers use operations such
as `changeEnd`, `cancel`, or `complete` instead of modifying storage fields.

## Make uncertain changes fallible

Changing one end of a booking can break its date relationship, so reuse the constructor.

```fsharp
[<RequireQualifiedAccess>]
module Booking =

    let changeEnd newEnd booking =
        create
            { Start = start booking
              End = newEnd }
```

The type tells the caller that the change can be refused:

```fsharp
match Booking.changeEnd proposedEnd booking with
| Ok changed -> save changed
| Error BookingError.EndBeforeStart -> showDateError ()
```

## Keep preserving changes total

Some operations preserve the invariant by their construction. Shifting both dates by the same number of days cannot
reverse their order.

```fsharp
[<RequireQualifiedAccess>]
module Booking =

    let shift days booking =
        {
            Start = (start booking).AddDays days
            End = (finish booking).AddDays days
        }
```

This function can return `Booking` directly. Keep it inside the module that can see the private representation.

## Use drafts for several user edits

An edit screen often changes several fields before submission. Convert to a draft, edit it, then call the constructor
once.

```fsharp
let edited =
    booking
    |> Booking.toDraft
    |> fun draft ->
        { draft with
            Start = proposedStart
            End = proposedEnd }

match Booking.create edited with
| Ok booking -> save booking
| Error error -> redisplay edited error
```

Do not pass the draft into business functions that expect the invariant to hold.

## Use types for important lifecycle states

When operations differ sharply by state, use separate types so unavailable transitions are absent from the interface.

```fsharp
type DraftOrder
type SubmittedOrder
type CancelledOrder

[<RequireQualifiedAccess>]
module Order =
    val submit:
        DraftOrder ->
        Result<SubmittedOrder, SubmitError>

    val cancel:
        SubmittedOrder ->
        Result<CancelledOrder, CancelError>
```

`Order.submit cancelledOrder` cannot compile because the function requires `DraftOrder`.

Use this for lifecycle states that change what operations are legal. A single discriminated union field is simpler when
all states share the same operations and callers already handle them by pattern matching.

## Keep operation policies separate

A transition belongs to the aggregate when the rule is always true for those related values. Tenant permissions,
current time, feature flags, and operation-specific approval rules belong outside it.

Apply those requirements with ordinary result-returning functions or a Flow policy after intrinsic construction has
succeeded.
