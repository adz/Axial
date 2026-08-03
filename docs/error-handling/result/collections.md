---
weight: 50
title: Working with collections
description: Apply a fallible operation across a sequence with traverse and sequence.
---

# Working with collections

Applying a fallible operation to every item in a list leaves you with a list of results, which is rarely what you
want. `traverse` and `sequence` turn that inside out: one result holding every value.

```fsharp
open System
open Axial.Result

type SignupError =
    | AgeMissing
    | AgeNotANumber of string
    | AgeOutOfRange of int

let parseAge (raw: string) =
    Int32.TryParse raw
    |> Result.fromTry
    |> Result.orError (AgeNotANumber raw)
    |> Result.bind (fun age ->
        if age >= 0 && age < 130 then Ok age else Error (AgeOutOfRange age))
```

## traverse

`traverse` maps each item with a `Result`-returning function and collects the successes.

```fsharp
[ "1"; "2"; "3" ] |> Result.traverse parseAge
// Ok [1; 2; 3]

[ "1"; "abc"; "500" ] |> Result.traverse parseAge
// Error (AgeNotANumber "abc")
```

## sequence

`sequence` is the same operation when you already hold the results — it is `traverse id`.

```fsharp
[ Ok 1; Ok 2 ] |> Result.sequence
// Ok [1; 2]

[ Ok 1; Error AgeMissing; Ok 3 ] |> Result.sequence
// Error AgeMissing
```

## It stops at the first error

Traversal is fail-fast, and this is observable: the mapping does not run for items after the failure. This matters
when the mapping does real work — a lookup, a request, a write.

```fsharp
let mutable visited = []

let recordAndParse raw =
    visited <- raw :: visited
    parseAge raw

[ "1"; "abc"; "3" ] |> Result.traverse recordAndParse
// Error (AgeNotANumber "abc")

List.rev visited
// ["1"; "abc"]
```

`"3"` was never visited. Only the first failure is reported, and later items are not examined at all — so this cannot
tell a user everything wrong with their input. To report every failure, see
[collecting every error](../collecting-errors/).

## Shape in and shape out

Both take any `seq<_>` and produce a **list**:

```fsharp
Result.traverse : ('a -> Result<'b, 'e>) -> seq<'a> -> Result<'b list, 'e>
Result.sequence : seq<Result<'a, 'e>> -> Result<'a list, 'e>
```

An array or a `seq` goes in; a list comes out. Convert afterwards with `Result.map` when you need a different shape:

```fsharp
[| "1"; "2" |] |> Result.traverse parseAge |> Result.map Array.ofList
// Ok [|1; 2|]
```

Both fully enumerate the input up to the failure point, so an infinite sequence will not terminate.
