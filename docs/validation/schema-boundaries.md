---
weight: 35
title: Schema Boundaries
description: Constructor invariants and contextual rules at schema boundaries.
---

# Schema Boundaries

This page shows where constructor invariants stop and contextual rules begin when schema input parsing creates trusted models.

Use a constructor invariant when the value would be invalid everywhere in the system. Use a contextual rule when the same
model can be valid in one workflow and unacceptable in another.

## Constructor Invariants

A constructor invariant protects the model's own meaning. If it fails, Axial does not have a trusted model.

```fsharp
type DateRange =
    private
        { Start: DateOnly
          End: DateOnly }

    static member Create start endDate =
        if start <= endDate then
            Ok { Start = start; End = endDate }
        else
            Error "End date must be on or after start date."

let dateRangeSchema =
    Schema.recordFor<DateRange, _> DateRange.Create
    |> Schema.date "start" _.Start
    |> Schema.date "end" _.End
    |> Schema.buildResult
```

`DateRange.Create` belongs in the schema because a range whose end is before its start is never a valid `DateRange`.
During input parsing, Axial first parses and checks each field. It calls the constructor only after every constructor
argument is already trusted. Field diagnostics gate constructor diagnostics, so a malformed `start` field does not also
produce a cross-field constructor error.

Constructor errors attach to the current object path by default. Use `Input.constructorErrorAt` when a specific relative
field path gives better boundary feedback:

```fsharp
let parsed =
    Input.parseWith (Input.constructorErrorAt "end") dateRangeSchema raw
```

## Contextual Rules

A contextual rule does not define whether the model can exist. It defines whether a trusted model is acceptable for a
specific action, user, time, tenant, feature flag, or external state.

Keep contextual rules outside the constructor when they depend on workflow context:

- a support ticket can exist while closed, but the "add reply" workflow may require it to be open
- an order can exist below a shipping threshold, but the "free shipping" workflow may require a minimum total
- a date range can be valid by itself, but a booking workflow may reject dates outside the current booking window

Run those checks after parsing has produced a model. That keeps schemas responsible for model construction and lets each
workflow choose its own policy without weakening the model constructor.

```fsharp
type BookingContext =
    { EarliestStart: DateOnly
      LatestEnd: DateOnly }

let checkBookingWindow context (range: DateRange) =
    if range.Start >= context.EarliestStart && range.End <= context.LatestEnd then
        Ok range
    else
        Error "Date range is outside the booking window."
```

## Choosing The Boundary

Ask whether the rule needs information that is not part of the model's own fields and meaning.

Use a constructor invariant when:

- every instance must satisfy the rule
- failing the rule means there is no trusted model
- the rule belongs with the model's smart constructor
- the rule should run only after field parsing and field constraints succeed

Use a contextual rule when:

- the rule changes by workflow, user, tenant, clock, feature flag, inventory, or external service
- the model remains meaningful even when the action is rejected
- different applications or commands may apply different requirements to the same model
- the check should run over an already trusted model

