---
title: Agent Guide
description: Direct guidance for Result, Check, and Refined APIs.
---

# Agent Guide

`Axial.ErrorHandling` installs `Axial.Result` and `Axial.Refined`.

- Return ordinary `Result<'value,'error>` from domain functions.
- Use `result { }` when a later step depends on an earlier success.
- Use `Check<'value>` for reusable rules over one already-typed value. Checks contain no input paths.
- Use named `Parse` functions for serialized primitive input.
- Use named `Refine` functions for built-in refined values.
- Introduce `refine { }` with explicit `Parse` and `Refine` results before using type-directed `let!`.
- Use a private wrapper and smart constructor when success should be visible in the type.
- Store construction and inspection together with `Refinement.define`.
- Add a static `Refinement` member to make an application type work with `Refine.from` and `refine { }`.

```fsharp
let count = Parse.int "42"
let name = Refine.nonBlankString "Ada"
```

Start with [Refined](./refined/). See [Define Refined Types](./refined/domain-values/) after the built-in constructors.
