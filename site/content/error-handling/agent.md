---
title: Agent Guide
description: Direct guidance for Result, Check, and Refined APIs.
type: docs
---


`Axial.ErrorHandling` installs `Axial.Result` and `Axial.Refined`.

- Return ordinary `Result<'value,'error>` from domain functions.
- Use `result { }` when a later step depends on an earlier success.
- Use `Check<'value>` for reusable rules over one already-typed value. Checks contain no input paths.
- Use a private wrapper and smart constructor when success should be visible in the type.
- Store construction and inspection together with `Refinement.define`.
- Add a static `Refinement` member to make an application type work with `Refine.from` and `refine { }`.
- Put accumulated field, index, map-key, and nested failures in Schema. `Schema.parse` and `Schema.check` return
  `SchemaErrors`.

```fsharp
let customerId : Result<CustomerId, RefinementError> =
    Refine.from rawCustomerId
```

For a complete contributed type, start with [Define Refined Types](./refined/domain-values/).
