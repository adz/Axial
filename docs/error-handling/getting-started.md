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
separate `Result` wrapper is needed; [Checks](./checks/) shows the rest, and the [Result Builder](./result-builder/)
composes fail-fast steps with `result {}`.

## Where To Go Next

- Stay here while the code is pure and one failure is enough to stop the operation.
- When the input is a whole domain model rather than one value, declare a [Schema]({{< relref "/schema/" >}}) — an
  invalid model is then never constructed.
- When the code needs dependencies, async or task work, cancellation, or runtime policy, lift it into
  [Flow]({{< relref "/flow/" >}}).
