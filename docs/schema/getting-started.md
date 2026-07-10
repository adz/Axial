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

## Declare A Schema

Open `Axial.Schema.DSL` inside the module that defines the schema, and write it bare:

```fsharp
module SignupSchema =
    open Axial.Schema.DSL

    type Signup = { Email: string; Age: int }

    let schema =
        recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
        |> text [ required; email ] "email" _.Email
        |> int [ atLeast 13 ] "age" _.Age
        |> build
```

`text` and `int` are field shortcuts — each is `field [...] name getter Value.text`/`Value.int` with the value
schema already filled in, so a field line reads as "external name, getter, constraints" and nothing else. The same
shortcuts exist for `decimal`, `bool`, `date`, and the other primitive value shapes; reach for the general `field`
combinator directly when a field needs an explicit `ValueSchema<'field>` the shortcuts don't cover — a nested model,
a collection, or a refined value.

## The DSL Is Scoped On Purpose

`Axial.Schema.DSL` exists to be opened inside exactly one module: the one that defines a schema, the same way
`Axial.ErrorHandling.CheckDSL` is scoped to a module that runs checks. Opened there, it puts every constraint
(`required`, `email`, `atLeast`, ...), every field shortcut (`text`, `int`, `decimal`, `bool`, ...), and the builder
entry/exit points (`recordFor`, `field`, `build`) into scope unqualified, so a schema reads as one flat, purpose-built
vocabulary instead of a wall of `Schema.`/`SchemaConstraint.`/`Value.` prefixes.

That convenience has a cost: `int`, `decimal`, and `bool` shadow the F# core conversion functions of the same names.
Opening the DSL anywhere broader than the schema module — at file or namespace scope, in general application code —
means every unrelated `int x` in that scope now means something else. Keep the `open` local to the schema module, the
way the example above does, and it never comes up.

Outside a schema module, use the qualified form the DSL expands to:

```fsharp
open Axial.Schema

Schema.recordFor<Signup, _> (fun email age -> { Email = email; Age = age })
|> Schema.fieldWith [ SchemaConstraint.required; SchemaConstraint.email ] "email" _.Email Value.text
|> Schema.fieldWith [ SchemaConstraint.atLeast 13 ] "age" _.Age Value.int
|> Schema.build
```

Both forms produce the identical `Schema<Signup>` — the DSL is sugar over `Schema`/`SchemaConstraint`/`Value`, not a
separate implementation. See [The Schema DSL](./dsl/) for the full field and constraint vocabulary.

## Parse Raw Input

```fsharp
open Axial.Schema

let raw = RawInput.ofNameValues [ "email", "ada@example.com"; "age", "36" ]
let parsed = Model.parse SignupSchema.schema raw

match parsed.Result with
| Ok signup -> printfn "trusted: %A" signup
| Error _ -> printfn "rejected: %A" parsed.Errors   // path-aware; raw input kept in parsed.Input
```

The same schema also re-validates existing values, powers contextual rules, and describes itself to JSON Schema,
docs, and UI interpreters. Continue with the [tutorials](./tutorials/) — they build up nested models, collections,
rules, and metadata inspection step by step.

## Related

- For a single value rather than a whole model, use [Refined](./refined/) types or plain `Result` in
  [Error Handling]({{< relref "/error-handling/" >}}).
- To carry a parsed model into a workflow with dependencies and async work, see
  [Flow]({{< relref "/flow/" >}}).
