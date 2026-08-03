---
weight: 70
title: The result computation expression
description: Write dependent fallible steps as straight-line code with result { }.
type: docs
---


`result { }` connects steps that each return a `Result`. Each success passes its value to the next line; the first
`Error` becomes the result of the whole block and the remaining lines do not run.

```fsharp
open System
open Axial.Result

type SignupError =
    | NameMissing
    | AgeNotANumber of string
    | AgeOutOfRange of int

type Signup = { Name: string; Age: int }
```

## What the keywords do

`let!` binds the value inside `Ok` to the name on its left. `do!` runs a step whose success value is `unit`, so there
is no name. `return` wraps a plain value back up as `Ok`. `return!` uses a complete `Result` as the block's result.

```fsharp
result {
    let! name = parseName "Ada"
    let! age = parseAge "36"
    return { Name = name; Age = age }
}
```

```text
Ok { Name = "Ada"; Age = 36 }
```

The same block with the types written out, to show what is on each side of the binding:

```fsharp
result {
    let! (name: string) = (parseName "Ada": Result<string, SignupError>)
    let! (age: int) = (parseAge "36": Result<int, SignupError>)
    return { Name = name; Age = age }
}
// Result<Signup, SignupError>
```

On the right of `let!` is a `Result<'value, 'error>`; on the left is the `'value`. The block's own type is
`Result<'whatever you return, 'error>` — the error type is shared by every step, which is why they all fail with
`SignupError` here.

## Failure stops the block

```fsharp
let signup name age =
    result {
        let! name = parseName name
        let! age = parseAge age
        return { Name = name; Age = age }
    }

signup "Ada" "36"    // Ok { Name = "Ada"; Age = 36 }
signup "" "36"       // Error NameMissing
signup "Ada" "abc"   // Error (AgeNotANumber "abc")
```

`signup "" "abc"` returns `Error NameMissing`. The age is never parsed, so its failure is never seen — the block stops
at the first one.

That short-circuit is observable, not just a description of the result:

```fsharp
let mutable calls = 0

let track raw =
    calls <- calls + 1
    parseAge raw

result {
    let! first = parseAge "abc"
    let! second = track "36"
    return first + second
}
// Error (AgeNotANumber "abc")

calls
// 0
```

`track` never ran.

## Control flow inside a block

The builder supports the ordinary constructs, so a block is not restricted to a straight run of bindings:

```fsharp
result {
    use reader = openReader path          // disposed on the way out, success or failure
    let! header = readHeader reader

    for line in lines do                  // stops at the first failing iteration
        do! validate line

    while not (isDone ()) do
        do! step ()

    return header
}
```

`try/with` and `try/finally` work as usual. Note what they do and do not catch: they handle .NET **exceptions**, not
`Error` values. An `Error` is an ordinary return value, so it does not trigger `with` — it just ends the block.

## When to use it

- a later step needs a value bound by an earlier one;
- continuing after a failure makes no sense;
- the pipeline has grown past two or three steps and the nesting from `bind` is getting hard to read.

When the steps are independent of each other and the caller should hear about all the failures rather than the first,
use [collecting every error](../collecting-errors/) instead.
