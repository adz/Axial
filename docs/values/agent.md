---
title: For AI agents
description: High-signal Constraint, Refined, and Parse guidance for coding agents.
weight: 100
---

# For AI agents

Values is a navigation grouping over three packages that admit values: `Axial.Constraint`, `Axial.Refined`, and
`Axial.Parse`. There is no `Axial.Values` package and no `Axial.Values` namespace. `Axial.Refined` depends on
`Axial.Constraint`; `Axial.Parse` depends on neither; none depends on `Axial.Result`.

- Use `Constraint<'value>` to test one typed value without replacing it.
- Use `Constraint<'value>` when executable checking and portable metadata must stay together.
- Use `Constraint.guard` to keep the original value after a successful check.
- Use `Parse.*` only to decode serialized primitive input.
- Use `Refinement<'underlying,'refined>` only for invariant-carrying destination types with a total reverse projection.
- Map `ParseError` and `Violation` into the application's error type at composition boundaries.
- Use `Schema.refine` for a refinement, `Schema.convert` for total mappings, `Schema.tryConvert` for fallible
  mappings, and `Schema.admit` for structured draft-to-domain construction.

```fsharp
open Axial.Constraint
open Axial.Parse
open Axial.Refined
open Axial.Result

type InputError =
    | InvalidCount of ParseError
    | NonPositiveCount of Violation

let count raw =
    result {
        let! parsed = Parse.int raw |> Result.mapError InvalidCount
        let! count = parsed |> Constraint.guard (Constraint.greaterThan 0) |> Result.mapError NonPositiveCount
        return count
    }
```

The `result { }` above is [`Axial.Result`]({{< relref "/result/" >}}) and is optional — these packages return the
standard F# `Result`, so any Result vocabulary composes them.

Start with [Getting Started](./getting-started/), then read [Define Refined Types](./refined/domain-values/).

For compact prompt context, load [`/values/llms.txt`](/values/llms.txt).
