---
title: Agent Guide
description: Direct guidance for Result, Check, Parse, and Refined APIs.
---

# Agent Guide

`Axial.ErrorHandling` installs `Axial.Result`, `Axial.Check`, `Axial.Parse`, and `Axial.Refined`. It exposes no API.

- Return ordinary `Result<'value,'error>` from application and domain functions.
- Use `result { }` when later work depends on earlier success.
- Use `Check<'value>` to test one typed value without replacing it.
- Use `Constraint<'value>` when executable checking and portable metadata must stay together.
- Use `Result.guard` to keep the original value after a successful check.
- Use `Parse.*` only to decode serialized primitive input.
- Use `Refinement<'underlying,'refined>` only for invariant-carrying destination types with a total reverse projection.
- Map `ParseError` and `CheckFailure list` into the application's error type at composition boundaries.
- Use `Schema.refine` for a refinement, `Schema.convert` for total mappings, `Schema.tryConvert` for fallible mappings, and `Schema.admit` for structured draft-to-domain construction.

```fsharp
open Axial.Check
open Axial.Parse
open Axial.Refined
open Axial.Result

type InputError =
    | InvalidCount of ParseError
    | NonPositiveCount of CheckFailure list

let count raw =
    result {
        let! parsed = Parse.int raw |> Result.mapError InvalidCount
        let! count = Check.greaterThan 0 parsed |> Result.map (fun () -> parsed) |> Result.mapError NonPositiveCount
        return count
    }
```

Start with [Error Handling](./overview/), then read [Define Refined Types](./refined/domain-values/).
