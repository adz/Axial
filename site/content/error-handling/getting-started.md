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
| Parse serialized text | `Parse.int`, `Parse.guid`, and other `Parse` functions | `Result<'value, ParseError>` |
| Construct a type that records a successful check | `Refine.nonBlankString`, `Refine.positiveInt`, and other `Refine` functions | `Result<'value, RefinementError>` |

`Result` is the common return type. `Check` preserves the checked value and can report several failures about that one
value. A refinement changes the type, so later code knows construction succeeded.

```fsharp
let parsed = Parse.int "12"
let refined = Refine.positiveInt 12
```

## Continue

- [Result](./result/): fail-fast composition and extraction helpers.
- [Check](./checks/): reusable constraints over one value.
- [Refined](./refined/): parsing, built-in refined values, dependent construction, and application-defined types.
