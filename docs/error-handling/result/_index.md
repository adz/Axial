---
weight: 8
title: Result
type: docs
description: Compose operations that can fail, using the standard F# Result type.
---

# Result

`Axial.Result` works with the standard F# `Result<'value, 'error>`. It does not wrap or replace that type: a value
produced by these helpers is an ordinary `Ok` or `Error` that any other F# code can pattern match.

What the package supplies is the vocabulary around it — turning ordinary values into a `Result` carrying your own
error type, chaining steps that each may fail, replacing one error with another, getting values back out, and two
computation expressions for writing sequences of fallible steps as straight-line code.

The package is a standalone leaf with no dependency on any other Axial package, and nothing in this section uses one.

```sh
dotnet add package Axial.Result
```

## A first example

```fsharp
open System
open Axial.Result

type SignupError =
    | NameMissing
    | AgeNotANumber of string
    | AgeOutOfRange of int

let parseName raw =
    raw
    |> Result.failIf String.IsNullOrWhiteSpace
    |> Result.orError NameMissing

let parseAge raw =
    Int32.TryParse raw
    |> Result.fromTry
    |> Result.orError (AgeNotANumber raw)
    |> Result.bind (fun age ->
        if age >= 0 && age < 130 then Ok age else Error (AgeOutOfRange age))

result {
    let! name = parseName "Ada"
    let! age = parseAge "36"
    return {| Name = name; Age = age |}
}
```

```text
Ok { Name = "Ada"; Age = 36 }
```

Every page below builds on this same `parseName`/`parseAge` pair, so the examples compose with each other.

## Pages

- [Creating a Result](./creating/) — turn options, nullables, `TryParse` tuples, booleans, and predicates into a
  `Result` carrying your own error type.
- [Transforming values](./transforming/) — `map` and `bind`, and how a chain of fallible steps composes.
- [Handling errors](./handling-errors/) — change the error type, replace one error with another, and recover.
- [Extracting values](./extracting/) — get back to a plain value, an option, or a default.
- [Working with collections](./collections/) — apply a fallible operation across a sequence with `traverse` and
  `sequence`.
- [Observing a Result](./observing/) — log or measure mid-pipeline with `tap` and `tapError`.
- [The result computation expression](./result-ce/) — write dependent steps as straight-line code with `result { }`.
- [Collecting every error](./collecting-errors/) — report all independent failures at once with `result.list { }`
  and `and!`.
- [API reference](../reference/result/) — every function, generated from the source.
