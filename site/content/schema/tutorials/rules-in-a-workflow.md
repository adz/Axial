---
weight: 20
title: Rules And Policies Tutorial
description: Accept a trusted model into one workflow but not another.
type: docs
---


This tutorial takes a trusted support ticket and applies workflow-specific requirements: triage accepts it, approval
does not. The ticket itself stays valid throughout — rules judge acceptability, not existence.

## The Trusted Model

Assume `Ticket` came from `Schema.parse`, `Schema.check`, or a smart constructor, so its intrinsic constraints
already hold:

```fsharp
open Axial.Validation
open Axial.Schema

type Ticket = { Priority: int; HasAssignee: bool }

type TicketRuleError =
    | HighPriorityNeedsAssignee
    | ManualReviewRequired
```

## Write The Rules

A rule accepts the model or returns diagnostics. Attach failures to a field path so they render like input errors:

```fsharp
let needsAssignee ticket =
    if ticket.Priority >= 4 && not ticket.HasAssignee then
        ContextRules.failAt [ PathSegment.Name "assignee" ] HighPriorityNeedsAssignee
    else
        Ok ()

let needsReview ticket =
    if ticket.Priority >= 5 then ContextRules.fail ManualReviewRequired else Ok ()
```

## Compose Rule Sets Per Workflow

```fsharp
let triageRules = [ needsAssignee ]

let approvalRules =
    [ needsAssignee
      ContextRules.name "review" needsReview ]
```

## Apply

`ContextRules.apply` returns the same trusted instance or accumulated diagnostics:

```fsharp
let ticket = { Priority = 5; HasAssignee = true }

ContextRules.apply triageRules ticket      // Ok ticket
ContextRules.apply approvalRules ticket    // Error — review required at path "review"
```

## Run It Inside A Workflow

Wrap the rule set in a `Policy` when the requirement should read the workflow environment or compose with other steps,
then run it with `Flow.verify`:

```fsharp
open Axial.Flow

type ApprovalEnv = { RequireReview: bool }

let reviewPolicy : Policy<ApprovalEnv, TicketRuleError, Ticket, Ticket> =
    Policy.context
        (fun env ticket ->
            let rules = if env.RequireReview then approvalRules else triageRules
            ContextRules.apply rules ticket)
        (fun diagnostics -> diagnostics |> Diagnostics.flatten |> List.head |> _.Error)

let approve ticket = flow {
    let! accepted = ticket |> Flow.verify reviewPolicy
    return accepted
}
```

`Flow.verify` injects the current environment and short-circuits the flow on failure, like any failing bind.

## Next

- [Rules](../../rules/) for the full guide, including custom codes and scoping combinators.
- The runnable [policy example]({{< relref "/schema/examples.md" >}}) adapting all five verification boundaries.
