---
weight: 10
title: Getting Started
description: Compose operations that can fail into one application error type, using the standard F# Result.
---

# Getting Started

`Axial.Result` is a standalone leaf. Nothing else in Axial is required, and it depends on no other Axial package:

```bash
dotnet add package Axial.Result
```

```fsharp
open Axial.Result
```

The package works with the standard F# `Result<'value, 'error>` — it does not wrap or replace it. What it supplies is
the vocabulary around it:

| Concern | API | Returns |
| --- | --- | --- |
| Turn an option, nullable, or `TryParse` tuple into a Result | `Result.orError`, `Result.fromTry` | `Result<'value,'error>` |
| Compose dependent failures | `result { }` | `Result<'value,'error>` |
| Collect independent failures | `result.list { }` with `and!` | `Result<'value,'error list>` |
| Apply a fallible operation across a sequence | `Result.traverse`, `Result.sequence` | `Result<'value list,'error>` |
| Observe without changing the value | `Result.tap`, `Result.tapError` | `Result<'value,'error>` |

## Compose dependent steps

```fsharp
type QuantityError =
    | InvalidInteger of string
    | NotPositive of int

let quantity raw =
    result {
        let! parsed =
            Int32.TryParse raw
            |> Result.fromTry
            |> Result.orError (InvalidInteger raw)

        let! positive =
            if parsed > 0 then Ok parsed else Error (NotPositive parsed)

        return positive
    }
```

Each step's own failure is mapped into one deliberate application error type at the bind site.

## Collect independent failures

`let!` fails fast; `and!` accumulates. The boundary is the compiler's, not a setting:

```fsharp
result.list {
    let! name = parseName input.Name
    and! age  = parseAge input.Age
    return name, age
}
// Error [ NameMissing; AgeNotANumber "x" ]
```

Everything inside one `and!` group is accumulated into a list. A later `let!` binds sequentially and fails fast, because
it can depend on the earlier results. See [Collecting every error](./collecting-errors/) for the exact boundary.

## Where the other packages fit

This accumulation is **flat**: a list of your error values with no field identity and no path. Admitting values in the
first place belongs to the [Values]({{< relref "/values/" >}}) packages, and path-aware accumulated diagnostics over a
whole structured input belong to [Axial.Schema]({{< relref "/schema/" >}}).

All of them return the standard F# `Result`, so these helpers work on their output — but none of them requires this
package.

## Continue

- [Creating a Result](./creating/)
- [Transforming values](./transforming/)
- [Handling errors](./handling-errors/)
- [The result computation expression](./result-ce/)
- [Collecting every error](./collecting-errors/)
- [Comparison with FsToolkit.ErrorHandling](./fstoolkit-comparison/)
