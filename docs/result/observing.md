---
weight: 60
title: Observing a Result
description: Log or measure a Result mid-pipeline without changing it.
---

# Observing a Result

`tap` and `tapError` run a side effect and hand the result back unchanged. They exist so logging does not force you to
break a pipeline apart.

```fsharp
open System
open Axial.Result

type SignupError =
    | AgeNotANumber of string
    | AgeOutOfRange of int
```

## Without them

Logging mid-pipeline means naming the intermediate value and returning it again:

```fsharp
let logged =
    let outcome = parseAge raw
    match outcome with
    | Ok age -> printfn "accepted %d" age
    | Error failure -> printfn "rejected: %A" failure
    outcome
```

## With them

```fsharp
parseAge "abc"
|> Result.tap (fun age -> printfn "accepted %d" age)
|> Result.tapError (fun failure -> printfn "rejected: %A" failure)
```

```text
rejected: AgeNotANumber "abc"
```

The value returned is the original `Error (AgeNotANumber "abc")`. `tap` did not run, because the result was not `Ok`;
`tapError` ran and returned its input untouched.

Both signatures say the same thing — the effect returns `unit`, so it has no way to influence what comes out:

```fsharp
Result.tap      : ('value -> unit) -> Result<'value, 'error> -> Result<'value, 'error>
Result.tapError : ('error -> unit) -> Result<'value, 'error> -> Result<'value, 'error>
```

## Where they earn their place

At a boundary, where you want a record of what happened but the caller still gets the untouched result:

```fsharp
let handleSignup raw =
    parseAge raw
    |> Result.tapError (fun failure -> logger.Warning("signup rejected: {Failure}", failure))
    |> Result.map buildAccount
```

Keep the effect small and total. An effect that throws will propagate out of `tap`, which defeats the purpose of
working in `Result` — and because the exception escapes mid-pipeline, the result you were carrying is lost.
