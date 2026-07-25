---
weight: 10
title: Getting Started
description: Use Result, Check, and Refined values for typed failures and domain construction.
type: docs
---


Install the focused package you need, or the combined package for all three:

```bash
dotnet add package Axial.Result    # Result composition and result { }
dotnet add package Axial.Check     # reusable, path-free value checks
dotnet add package Axial.Refined   # parsing and refined domain values
dotnet add package Axial.ErrorHandling   # installs all three
```

`Axial.ErrorHandling` installs `Axial.Result`, `Axial.Check`, and `Axial.Refined` and exposes no API of its own —
open the package you need directly.

```fsharp
open Axial.Result
open Axial.Check
open Axial.Refined
```

Already use FsToolkit.ErrorHandling or your own Result helpers? `Axial.Check` and `Axial.Refined` do not depend on
`Axial.Result`, so you can add either (or both) without opening `Axial.Result` or creating builder/module ambiguity.

## The three layers

| Problem | API | Result |
| --- | --- | --- |
| Sequence dependent operations that may fail | [`result { }`]({{< relref "/error-handling/reference/result/result-ce/" >}}) | `Result<'value, 'error>` |
| Describe and run reusable rules over one typed value | [`Check<'value>`]({{< relref "/error-handling/reference/check/" >}}) | `Result<'value, [`CheckFailure`]({{< relref "/error-handling/reference/check/t-check-checkfailure.md" >}}) list>` |
| Parse serialized text | [`Parse.int`]({{< relref "/error-handling/reference/refined/parse/m-refined-parse-int.md" >}}), [`Parse.guid`]({{< relref "/error-handling/reference/refined/parse/m-refined-parse-guid.md" >}}), and other [`Parse`]({{< relref "/error-handling/reference/refined/parse/" >}}) functions | `Result<'value, [`ParseError`]({{< relref "/error-handling/reference/refined/types/t-refined-parseerror.md" >}})>` |
| Construct a type that records a successful check | [`Refine.nonBlankString`]({{< relref "/error-handling/reference/refined/refine/m-refined-refine-nonblankstring.md" >}}), [`Refine.positiveInt`]({{< relref "/error-handling/reference/refined/refine/m-refined-refine-positiveint.md" >}}), and other [`Refine`]({{< relref "/error-handling/reference/refined/refine/" >}}) functions | `Result<'value, [`RefinementError`]({{< relref "/error-handling/reference/refined/types/t-refined-refinementerror.md" >}})>` |

`Result` is the common return type. `Check` preserves the checked value and can report several failures about that one
value. A refinement changes the type, so later code knows construction succeeded.

```fsharp
let parsed = Parse.int "12"
let refined = Refine.positiveInt 12
```

## Continue

- [Result](./result/): fail-fast composition and extraction helpers.
- [Check](./check/): reusable constraints over one value.
- [Refined](./refined/): parsing, built-in refined values, dependent construction, and application-defined types.
