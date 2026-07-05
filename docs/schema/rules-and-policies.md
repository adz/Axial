---
weight: 30
title: Rules And Policies
type: docs
description: Contextual rules over trusted models and environment-aware Flow policies.
---

# Rules And Policies

Once parsing has produced a trusted model, two tools carry verification the rest of the way: contextual rules decide
whether the model is acceptable, and policies run any verification step inside a workflow.

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

## Policies

A `Policy<'env, 'error, 'input, 'output>` is a named verification step: `'env -> 'input -> Result<'output, 'error>`.
Policies adapt every verification shape into one workflow error type:

```fsharp
// contextual rules with environment access
let underQuantityCap : Policy<OrderEnv, OrderError, OrderLine, OrderLine> =
    Policy.context
        (fun env line -> Rules.apply (quantityCapRules env) line)
        mapDiagnostics

// schema input parsing
let parseOrderLine : Policy<OrderEnv, OrderError, RawInput, OrderLine> =
    Policy.lift (fun raw -> (Input.parse orderLineSchema raw).Result) mapDiagnostics
```

`Policy.compose` chains steps, `Policy.optional` switches a step on and off per environment, and `Flow.verify` runs a
policy inside `flow { }` with the current environment injected:

```fsharp
let acceptLine raw = flow {
    let! line =
        raw
        |> Flow.verify (Policy.compose parseOrderLine (Policy.optional _.EnforceQuantityCap underQuantityCap))
    return line
}
```

`Flow.verify` short-circuits the workflow on the first policy failure, like any other failing bind.

See the runnable [policy example]({{< relref "/patterns/examples/" >}}) for the full set of adapters — parsing,
refined construction, schema input, validation, and rules — composed into one workflow.

## Choosing Between Them

- requirement over a trusted model that varies by context → `RuleSet`, evaluated with `Rules.apply`
- running any verification inside a workflow, with environment access, composition, or per-environment switches →
  `Policy` with `Flow.verify`
- one-off error assignment at a single bind site → plain [`Bind`]({{< relref "/flow/bind/" >}})
