---
weight: 30
title: Rules
type: docs
description: Contextual requirements over an already-trusted model.
---


Parsing proves a model is well-formed once, at construction. It can't know that triage only needs an assignee while
approval also needs review — that's not a fact about the model's shape, it's a fact about which workflow is looking
at it right now. `Rules` is for exactly that: requirements over an already-trusted model that vary by context, without
touching how the model gets built.

## Contextual Rules

A `RuleSet<'model, 'error>` holds rules that evaluate an already-trusted model against workflow context. Rules never
construct or transform the model — `Rules.apply` returns the same instance or path-aware diagnostics.

```fsharp
type TicketRuleError =
    | HighPriorityNeedsAssignee
    | ManualReviewRequired

let approvalRules =
    Rules.concat
        [ Rules.create (fun ticket ->
              if ticket.Priority >= 4 && not ticket.HasAssignee then
                  Rules.failAt [ PathSegment.Name "assignee" ] HighPriorityNeedsAssignee
              else
                  Ok ())
          Rules.create (Rules.name "review" (fun ticket ->
              if ticket.Priority >= 5 then Rules.fail ManualReviewRequired else Ok ())) ]

match Rules.apply approvalRules ticket with
| Ok trusted -> approve trusted
| Error diagnostics -> reject diagnostics
```

Failures attach anywhere in the diagnostics tree — `Rules.failAt`, or the `Rules.name`/`Rules.key`/`Rules.index`
scoping combinators — and render exactly like schema input diagnostics, so one error-display layer serves both.
`Rules.custom` and `Rules.failCustom` produce `SchemaError.Custom` values with stable codes when the rule error type is
`SchemaError`.

Different workflows apply different rule sets to the same model: triage may only require an assignee while approval
also requires review. That is the point — the model's constructor stays strong, and each workflow states its own bar.

## Rules Inside A Workflow

`Rules.apply` is a plain function — `RuleSet<'model, 'error> -> 'model -> Result<'model, Diagnostics<'error>>` — so it
already binds directly in `result {}` or `flow {}` with no adapter needed for the common case. When a rule set needs
the workflow's environment, should compose with other verification steps (parsing, refined construction), or should
switch on and off per environment, that's `Policy` and `Flow.verify` — both live in the separate `Axial.Flow`
package, not here. See [Bind Versus Policy]({{< relref "/flow/bind/" >}}#bind-versus-policy) for that, and the
[Rules In A Workflow tutorial](../tutorials/rules-in-a-workflow/) for `Rules.apply` wrapped in a `Policy` end to end.
