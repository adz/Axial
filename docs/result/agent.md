---
title: For AI agents
description: High-signal Axial.Result guidance for coding agents.
weight: 100
---

# For AI agents

`Axial.Result` is a standalone leaf: it depends on no other Axial package, and nothing in Axial depends on it. It works
with the standard F# `Result<'value,'error>` and never wraps or replaces that type.

- Return ordinary `Result<'value,'error>` from application and domain functions.
- Use `result { }` when later work depends on earlier success.
- Use `result.list { }` / `result.array { }` with `and!` when independent failures should all be reported.
- `let!` fails fast, `and!` accumulates — the F# compiler decides this by desugaring `and!` through `MergeSources`
  and a later `let!` through `Bind`. It is not a setting.
- Accumulation here is flat: a container of error values with no path and no field identity. Path-aware accumulation
  is `Axial.Schema`'s, and is not interchangeable with this.
- Use `Result.traverse` / `Result.sequence` to apply a fallible operation across a sequence.
- Use `Result.tap` / `Result.tapError` to log or measure mid-pipeline without changing the value.
- Map foreign errors (`ParseError`, `Violation`, exceptions) into the application's error type at the bind site.

```fsharp
open Axial.Result

type SignupError =
    | NameMissing
    | AgeNotANumber of string

let signup rawName rawAge =
    result.list {
        let! name = parseName rawName
        and! age  = parseAge rawAge
        return {| Name = name; Age = age |}
    }
    // Result<_, SignupError list>
```

Start with [Result](./), then read [Collecting every error](./collecting-errors/).

For compact prompt context, load [`/result/llms.txt`](/result/llms.txt).
