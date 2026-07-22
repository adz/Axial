---
weight: 30
title: Validation
type: docs
description: Accumulating sibling failures with Validation and Diagnostics.
---


Use `Validation<'value, 'error>` when several independent checks should run and report all their failures. It is the
accumulating counterpart to a fail-fast `Result`.

Every failed Validation contains `Diagnostics<'error>`. Diagnostics is the error structure: it stores errors at the
current location and under child paths such as `email`, `address.city`, or `lines[2]`.

The relationship is:

```fsharp
Validation<'value, 'error>
// success: 'value
// failure: Diagnostics<'error>
```

`validate {}` builds a Validation. Use `and!` for independent checks; their Diagnostics trees are merged when more
than one check fails.

```fsharp
let validateRegistration name email =
    validate {
        let! validName = checkName name |> Validation.fromResult |> Validation.name "name"
        and! validEmail = checkEmail email |> Validation.fromResult |> Validation.name "email"
        return validName, validEmail
    }
```

If both checks fail, the result contains one Diagnostics value with failures under `name` and `email`. Flatten it at
an API boundary when a list is easier to return:

```fsharp
validateRegistration "" "not-an-email"
|> Validation.toResult
|> Result.mapError Diagnostics.flatten
// Error [ { Path = [ Name "email" ]; Error = ... }
//         { Path = [ Name "name" ]; Error = ... } ]
```

Schema uses the same Diagnostics type for parse and construction failures. Use Validation directly when values
already exist and several checks should run without declaring a schema.

If a failure should stop dependent work, an ordinary `Result` may express that more directly. Async execution and
dependency management are separate concerns; `Validation` can be used with or without [Flow]({{< relref "/flow/" >}}).

## Installation

`Axial.Diagnostics` installs as part of `Axial.ErrorHandling` and `Axial.Schema`.

Or install it individually:

```sh
dotnet add package Axial.Diagnostics
```

## Guides

- [Tutorials](./tutorials/): validate independent fields and return all sibling failures.
- [Validate Builder](./validate-builder/): accumulating validation with `validate {}` and `and!`.
- [Schema section]({{< relref "/schema/" >}}): portable model schemas, input parsing, redisplay, rules, and policies.
- [Diagnostics](./diagnostics/): the tree structure, path operations, flattening, and rendering.

## Interop

Use `Validation.fromResult` to bring an existing fail-fast result into validation, and `Validation.toResult` when a boundary expects ordinary `Result`. `Validation.fromResult` is the canonical result-to-validation bridge; Axial does not also expose `Validation.ofResult`.

`Check` and `Result` helpers are alternatives and building blocks in the same package. Convert between them when it
helps a boundary; there is no required progression from one abstraction to another.

## Reference

- [Validation API]({{< relref "/error-handling/reference/error-handling/" >}})
- [Diagnostics API]({{< relref "/error-handling/reference/diagnostics/" >}})
