---
weight: 30
title: Handling errors
description: Change the error type, replace one error with another, and recover from failure.
type: docs
---


The error channel gets the same treatment as the success channel: map it, replace it, or use it to produce a fallback.

```fsharp
open Axial.Result

type SignupError =
    | NameMissing
    | AgeMissing
```

## Changing the error

`mapError` transforms whatever the error carries. Use it when the existing error holds something worth keeping.

```fsharp
Error NameMissing |> Result.mapError (fun _ -> "name is required")
// Error "name is required"
```

`orError` discards the existing error and substitutes yours. Use it when the incoming error carries nothing — the
`unit` from `okIf`, `failIf`, or `fromTry`, or a low-level detail the caller should not see.

```fsharp
Error "index out of range" |> Result.orError NameMissing
// Error NameMissing
```

`okOr` and `errorOr` change the error type while also changing what success means. `okOr` replaces the error on a
result you only care about the success of; `errorOr` succeeds *with* the error value, for when the failure is the
thing you wanted.

```fsharp
Ok 3 |> Result.okOr NameMissing                                // Ok 3
(Error "boom": Result<int, string>) |> Result.errorOr "was ok" // Ok "boom"
```

## Recovering

`orElse` supplies a fallback result when the first one fails. The fallback is an already-computed value.

```fsharp
(Error AgeMissing: Result<int, SignupError>) |> Result.orElse (Ok 0)
// Ok 0

Ok 36 |> Result.orElse (Ok 0)
// Ok 36
```

`orElseWith` computes the fallback from the error, and only on failure. Use it when the fallback is expensive, or when
which fallback to use depends on why the first attempt failed.

```fsharp
(Error AgeMissing: Result<int, SignupError>) |> Result.orElseWith (fun _ -> Ok 0)
// Ok 0
```

Both keep the error type the same. A recovery step that fails with a *different* error type is not covered here —
convert with `mapError` first so both sides agree.

## Choosing between them

| Situation | Function |
| --- | --- |
| the error carries something worth keeping | `mapError` |
| the error is `unit` or an internal detail | `orError` |
| a fallback value is already to hand | `orElse` |
| the fallback is expensive or depends on the error | `orElseWith` |
| the failure is the value you want | `errorOr` |
