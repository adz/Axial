---
weight: 10
title: Creating a Result
description: Turn options, nullables, TryParse tuples, booleans, and predicates into a Result carrying your own error type.
---

# Creating a Result

Most failures start as something that is not a `Result`: an option, a `Nullable`, a `bool`, a `TryParse` tuple. These
helpers do the conversion and let you attach your own error type.

All examples share one error type:

```fsharp
open System
open Axial.Result

type SignupError =
    | NameMissing
    | AgeMissing
    | AgeNotANumber of string
    | AgeOutOfRange of int
```

## Directly

`ok` and `error` construct a `Result`. They exist so a pipeline can end in one without a type annotation; `Ok` and
`Error` work just as well.

```fsharp
Result.ok 42        // Ok 42
Result.error NameMissing  // Error NameMissing
```

## From a predicate over a value

`okIf` keeps the value when the predicate holds; `failIf` is its inverse. Both fail with `unit`, because at that point
there is nothing to say about the failure yet — you attach the reason with `orError`.

```fsharp
"Ada" |> Result.okIf (String.IsNullOrWhiteSpace >> not)
// Ok "Ada"

"" |> Result.okIf (String.IsNullOrWhiteSpace >> not)
// Error ()

"" |> Result.okIf (String.IsNullOrWhiteSpace >> not) |> Result.orError NameMissing
// Error NameMissing

"  " |> Result.failIf String.IsNullOrWhiteSpace |> Result.orError NameMissing
// Error NameMissing
```

Splitting the test from the reason keeps the predicate reusable. The same `String.IsNullOrWhiteSpace` serves every
field, and each call site names its own error.

## From a standalone bool

When the condition is already computed and there is no value to carry forward, use `requireTrue`. It succeeds with
`unit`.

```fsharp
true  |> Result.requireTrue NameMissing   // Ok ()
false |> Result.requireTrue NameMissing   // Error NameMissing
```

## From an option, voption, or Nullable

```fsharp
Some "Ada" |> Result.someOr NameMissing        // Ok "Ada"
None       |> Result.someOr NameMissing        // Error NameMissing

ValueSome 36 |> Result.valueSomeOr AgeMissing  // Ok 36

Nullable 36 |> Result.nullableOr AgeMissing    // Ok 36
Nullable () |> Result.nullableOr AgeMissing    // Error AgeMissing

"Ada" |> Result.notNullOr NameMissing          // Ok "Ada"
```

`noneOr` and `valueNoneOr` invert the test, for when the *absence* is what you require. They succeed with `unit`,
since there is no value to hand back.

```fsharp
None    |> Result.noneOr NameMissing      // Ok ()
Some "" |> Result.noneOr NameMissing      // Error NameMissing
```

## From TryParse and Choice

.NET `TryParse` methods return a `bool * 'value` tuple. `fromTry` converts one directly.

```fsharp
Int32.TryParse "36"  |> Result.fromTry     // Ok 36
Int32.TryParse "abc" |> Result.fromTry     // Error ()

Int32.TryParse "abc" |> Result.fromTry |> Result.orError (AgeNotANumber "abc")
// Error (AgeNotANumber "abc")
```

`fromChoice` converts an F# `Choice`, which some older APIs return.

```fsharp
Choice1Of2 42 |> Result.fromChoice           // Ok 42
Choice2Of2 NameMissing |> Result.fromChoice  // Error NameMissing
```

## From a sequence

`headOr` takes the first item, or fails when the sequence is empty.

```fsharp
[ "Ada"; "Grace" ] |> Result.headOr NameMissing   // Ok "Ada"
[]                 |> Result.headOr NameMissing   // Error NameMissing
```

## Checking without losing the value

A check that returns `Result<unit, _>` proves a fact but discards the subject. `guard` runs it and hands the original
value back on success, so the check can sit in the middle of a pipeline.

```fsharp
let positive value =
    if value > 0 then Ok () else Error (AgeOutOfRange value)

36 |> Result.guard positive    // Ok 36
-1 |> Result.guard positive    // Error (AgeOutOfRange -1)
```

Note the success type: `Result.guard` returns `Ok 36`, not `Ok ()`. That is the difference between `guard` and calling
`positive` directly.

## Which one to reach for

| Starting point | Function |
| --- | --- |
| a value and a predicate | `okIf` / `failIf`, then `orError` |
| a `bool` with no subject value | `requireTrue` |
| `'value option` | `someOr` (or `noneOr` to require absence) |
| `'value voption` | `valueSomeOr` / `valueNoneOr` |
| `Nullable<'value>` | `nullableOr` |
| a possibly-null reference | `notNullOr` |
| `bool * 'value` from `TryParse` | `fromTry` |
| `Choice<'value, 'error>` | `fromChoice` |
| a sequence you need one item from | `headOr` |
| a check that returns `Result<unit, _>` | `guard` |

Next: [transforming values](../transforming/) once you have a `Result`.
