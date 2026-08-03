---
weight: 20
title: Transforming values
description: map and bind, and how a chain of fallible steps composes.
---

# Transforming values

Two functions cover almost all of it. `map` changes the success value. `bind` runs a step that can itself fail.

```fsharp
open System
open Axial.Result

type SignupError =
    | AgeMissing
    | AgeNotANumber of string
    | AgeOutOfRange of int
```

## map: the step cannot fail

`map` applies a plain function to the success value. An `Error` passes straight through untouched — the function never
runs.

```fsharp
Ok 36 |> Result.map (fun age -> age + 1)
// Ok 37

(Error AgeMissing: Result<int, SignupError>) |> Result.map (fun age -> age + 1)
// Error AgeMissing
```

## bind: the step can fail

`bind` applies a function that returns a `Result`. Use it when the next step has its own way of failing.

```fsharp
let withinRange age =
    if age < 130 then Ok age else Error (AgeOutOfRange age)

Ok 36  |> Result.bind withinRange   // Ok 36
Ok 500 |> Result.bind withinRange   // Error (AgeOutOfRange 500)
```

The distinction is only the return type of the function you pass. If it returns `'b`, use `map`. If it returns
`Result<'b, _>`, use `bind`. Using `map` with a `Result`-returning function gives you a nested
`Result<Result<_,_>,_>`, which is the usual sign you wanted `bind`.

## Composing a pipeline

Because each helper takes the `Result` last, steps chain with `|>`:

```fsharp
let parseAge (raw: string) =
    Int32.TryParse raw
    |> Result.fromTry
    |> Result.orError (AgeNotANumber raw)
    |> Result.bind (fun age ->
        if age >= 0 && age < 130 then Ok age else Error (AgeOutOfRange age))
```

Three outcomes, one for each way through:

```fsharp
parseAge "36"    // Ok 36
parseAge "abc"   // Error (AgeNotANumber "abc")
parseAge "500"   // Error (AgeOutOfRange 500)
```

The chain short-circuits. Once a step produces `Error`, every later `map` and `bind` is skipped and that first error
is what comes out. Nothing downstream needs to test for it.

When a pipeline grows past two or three steps, or when a later step needs a value bound several steps earlier, the
[result computation expression](../result-ce/) says the same thing without the nesting.
