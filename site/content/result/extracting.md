---
weight: 40
title: Extracting values
description: Get back to a plain value, an option, or a default.
type: docs
---


At the edge of a pipeline you usually stop working in `Result` and hand a plain value to something that does not know
about it.

```fsharp
open Axial.Result

type SignupError = AgeMissing
```

## A default

`defaultValue` returns the success value, or the fallback you supply.

```fsharp
Ok 36 |> Result.defaultValue 0                                   // 36
(Error AgeMissing: Result<int, SignupError>) |> Result.defaultValue 0   // 0
```

## An option

`toOption` and `toValueOption` drop the error entirely.

```fsharp
Ok 36 |> Result.toOption                                          // Some 36
(Error AgeMissing: Result<int, SignupError>) |> Result.toOption    // None
(Error AgeMissing: Result<int, SignupError>) |> Result.toValueOption // ValueNone
```

These discard why the operation failed. That is the point when the caller genuinely does not care, but it is worth
being deliberate: once the error is gone it cannot be reported, logged, or returned to a user.

## Pattern matching

Nothing stops you matching the value directly, and for a final branch that handles both sides it is usually clearest:

```fsharp
match parseAge "abc" with
| Ok age -> printfn "age is %d" age
| Error failure -> printfn "rejected: %A" failure
```

A `Result` from this package is the standard F# type, so every existing technique applies — matching, `function`
shorthand, active patterns.

## Keeping the error

If you need the value *and* the reason it might be missing, do not extract at all. Keep the `Result` until the last
possible point, and let the caller decide. Extracting early is what forces the error back into a `null`, a sentinel,
or an exception.
