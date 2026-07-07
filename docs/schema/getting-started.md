---
weight: 2
title: Getting Started
description: Declare a schema once and parse raw input into a trusted model.
---

# Getting Started

When the input is a whole model rather than one value, checking values one by one falls apart: fail-fast drops sibling
errors, hand-rolled accumulation loses the field paths, and either way the record gets constructed before the checks
finish. Instead, declare a schema once and parse raw input through it. If any constraint fails, the model is never
constructed — you get path-aware errors for every failing field, and the original input is retained for redisplay.

```fsharp
open Axial.Schema
open Axial.Validation.Schema

type Signup = { Email: string; Age: int }

let signupSchema =
    Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
    |> Schema.fieldWith
        [ SchemaConstraint.required; SchemaConstraint.email ]
        "email" _.Email Value.text
    |> Schema.fieldWith [ SchemaConstraint.atLeast 13 ] "age" _.Age Value.int
    |> Schema.build

let raw = RawInput.ofNameValues [ "email", "ada@example.com"; "age", "36" ]
let parsed = Input.parse signupSchema raw

match parsed.Result with
| Ok signup -> printfn "trusted: %A" signup
| Error _ -> printfn "rejected: %A" parsed.Errors   // path-aware; raw input kept in parsed.Input
```

The same schema also re-validates existing values, powers contextual rules, and describes itself to JSON Schema, docs,
and UI interpreters. Continue with the [tutorials](./tutorials/) — they build up nested models, collections, rules,
and metadata inspection step by step.

Once schemas live in their own modules, [the Schema DSL](./dsl/) drops the qualified prefixes — the same schema above
becomes `text [ required; email ] "email" _.Email` after one `open Axial.Schema.DSL`.

## Where To Go Next

- For a single value rather than a whole model, use [Refined](./refined/) types or plain `Result` in
  [Error Handling]({{< relref "/error-handling/" >}}).
- To carry a parsed model into a workflow with dependencies and async work, move to
  [Flow]({{< relref "/flow/" >}}).
