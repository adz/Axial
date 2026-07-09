---
weight: 2
title: Getting Started
description: Plain F# Result with your own error type, without the boilerplate.
---

# Getting Started

Most validation code fails the same way: not through a missing framework, but through boilerplate. Null and blank
guards, option unwrapping, boolean conditions hand-rolled into `Error` branches — each team reinvents them, and most
checks don't deserve more machinery than that.

Axial's answer is to keep standard F# `Result<'value, 'error>` with your own error union and make it terse. No Axial
types appear in your signatures.

```fsharp
open Axial

type UserError = | NameTooShort

let validateName (name: string) : Result<string, UserError> =
    name
    |> Check.minLength 3
    |> Result.orError NameTooShort

// This is a standard F# Result.
let result = validateName "Ad" // Error NameTooShort
```

`Check.minLength` is one of many reusable named checks — it already keeps the input value on success, so no
separate `Result` wrapper is needed. `Result.orError` attaches your own error, discarding whatever `Check` produced.

## The Three Pieces

| You need | Reach for | Shape |
| --- | --- | --- |
| A raw `bool` for an `if`/`match` branch | [`Predicate`](./predicates/) | `bool` |
| A reusable, named constraint that becomes a typed failure | [`Check`](./checks/) | `Result<'value, CheckFailure list>` |
| To attach a domain error, extract a value, or sequence steps | [`Result`'s helpers and `result {}`](./result-builder/) | `Result<'value, 'error>` |

They compose in that order: `Predicate` answers "is this true right now," `Check` turns the same kind of fact into a
structured, reusable, pipeable result, and `Result` carries it the rest of the way to your domain error and through
the rest of the workflow.

## Guides

- [Predicates](./predicates/) covers the `bool` layer for local branching.
- [Checks](./checks/) covers the full `Check` surface, the `CheckDSL`, and composition with `Check.all`/`Check.any`.
- [Result Builder](./result-builder/) covers `result {}` for sequencing dependent fail-fast steps.
- The [tutorial](./tutorials/) walks through building a small validation flow end to end.
