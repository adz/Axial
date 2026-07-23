---
weight: 10
title: Getting Started
description: Use Result, Check, and Refined values for typed failures and domain construction.
type: docs
---


Install the combined package:

```bash
dotnet add package Axial.ErrorHandling
```

It installs `Axial.Result` and `Axial.Refined`. A project that only needs one part can reference that package directly.

```fsharp
open Axial.ErrorHandling
open Axial.Refined
```

## The three layers

| Problem | API | Result |
| --- | --- | --- |
| Sequence dependent operations that may fail | `result { }` | `Result<'value, 'error>` |
| Describe and run reusable rules over one typed value | `Check<'value>` | `Result<'value, CheckFailure list>` |
| Construct a type that cannot contain an invalid value | `Refinement<'raw,'value>`, `Refine.from`, `refine { }` | `Result<'value, RefinementError>` |

`Result` is the common return type. `Check` preserves the checked value and can report several failures about that one
value. A refinement changes the type, so later code knows construction succeeded.

```fsharp
let parseQuantity raw : Result<PositiveInt, RefinementError> =
    refine {
        let! (number: int) = raw
        let! (quantity: PositiveInt) = number
        return quantity
    }
```

Path-aware accumulation belongs to Schema because Schema already knows field names, collection indexes, map keys, and
nested structure. `Schema.parse` and `Schema.check` return `SchemaErrors`; application code does not attach paths by
hand.

## Continue

- [Result](./result/): fail-fast composition and extraction helpers.
- [Check](./checks/): reusable constraints over one value.
- [Refined](./refined/): wrappers, smart constructors, contributed refinements, and Schema integration.
- [Schema]({{< relref "/schema/" >}}): structured input and complete path-aware boundary failures.
