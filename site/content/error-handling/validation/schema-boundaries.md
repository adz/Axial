---
weight: 35
title: Schema Boundaries
description: Constructor invariants and contextual rules at schema boundaries.
type: docs
---


This page shows where constructor invariants stop and contextual rules begin when schema input parsing creates trusted models.

Use a constructor invariant when the value would be invalid everywhere in the system. Use a contextual rule when the same
model can be valid in one workflow and unacceptable in another.

## Constructor Invariants

A constructor invariant protects the model's own meaning. If it fails, Axial does not have a trusted model.

```fsharp
open Axial.Schema.DSL
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
    recordFor<DateRange, _> DateRange.Create
    |> field "start" _.Start date
    |> field "end" _.End date
    |> buildResult
```

`DateRange.Create` belongs in the schema because a range whose end is before its start is never a valid `DateRange`.
During input parsing, Axial first parses and checks each field. It calls the constructor only after every constructor
argument is already trusted. Field diagnostics gate constructor diagnostics, so a malformed `start` field does not also
produce a cross-field constructor error.

Constructor errors attach to the current object path by default. Use `Schema.constructorErrorAt` when a specific relative
field path gives better boundary feedback:

```fsharp
let parsed =
    Schema.parseWith (Schema.constructorErrorAt "end") dateRangeSchema raw
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

## Schema Constraints Versus Rules

Schema constraints and contextual rules answer different questions:

- a schema constraint is field-local and unconditional: `maxLength 100` on a subject is true for every ticket, in every
  workflow, and can run during input parsing before a model exists
- a contextual rule needs the completed, trusted model and describes when that model is acceptable for one workflow

Author contextual rules as plain functions in a list and evaluate them with `ContextRules.apply`:

```fsharp
type TicketRuleError =
    | HighPriorityNeedsAssignee
    | ManualReviewRequired

let approvalRules =
    [ (fun ticket ->
          if ticket.Priority >= 4 && not ticket.HasAssignee then
              ContextRules.failAt [ PathSegment.Name "assignee" ] HighPriorityNeedsAssignee
          else
              Ok ())
      ContextRules.name "review" (fun ticket ->
          if ticket.Priority >= 5 then
              ContextRules.fail ManualReviewRequired
          else
              Ok ()) ]

match ContextRules.apply approvalRules ticket with
| Ok trusted -> approve trusted
| Error diagnostics -> reject diagnostics
```

`ContextRules.apply` never constructs, parses, or transforms the model. On success it returns the same trusted instance; on
failure it returns path-aware diagnostics that render exactly like schema input diagnostics, so one error-display layer
serves both boundaries. Different workflows can apply different rule sets to the same model — triage may only require an
assignee while approval also requires review — without weakening the model's constructor.

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

