---
weight: 10
title: Check
description: Use structured Check programs before shaping failures with Result, Validation, or Flow.
---

# Check

`Check` contains reusable value constraints. A passing check hands back the same value unchanged, so it pipes
directly into the next step — no separate "keep the input" wrapper is needed.

## Start With The DSL

Open `Axial.ErrorHandling.CheckDSL` inside a module that runs several checks, and write them bare:

```fsharp
module SignupChecks =
    open Axial.ErrorHandling.CheckDSL

    let validateAge : Check<int> = atLeast 13
    let validateEmail : Check<string> = Check.all [ present; email ]
```

`present`, `atLeast`, `email`, `minLength`, and the rest of the deduplicated root names are all here. `Check.all`,
`Check.any`, `Check.not`, and `Check.mapFailure` stay qualified even with the DSL open — they, along with `contains`,
`distinct`, `length`, and `between`, shadow FSharp.Core names, so the DSL deliberately leaves them off its surface.

Only open this inside the module that runs the checks, the same way `Schema.DSL` is scoped to schema definition
modules — it isn't worth it for a single check call; reach for the qualified `Check.present value` form instead.

## Attach a Domain Error

`Check` failures are reusable structural facts. Map them to a domain error at the boundary with `Result.orError`
(discards whatever error was there and replaces it) or `Result.mapError` (transforms it):

```fsharp
type SignUpError =
    | NameRequired
    | AgeInvalid

let requireAdult age : Result<int, SignUpError> =
    age
    |> Check.atLeast 18
    |> Result.orError AgeInvalid

let requireName name : Result<string, SignUpError> =
    Check.present name |> Result.orError NameRequired
```

Some helpers already return a useful diagnostic error. Use `Result.mapError` for those:

```fsharp
type OrderError = InvalidQuantity of CheckFailure list

let quantity value : Result<int, OrderError> =
    value
    |> Check.greaterThan 0
    |> Result.mapError InvalidQuantity
```

## Check Versus Result's Generic Helpers

Use `Check` for anything with a name and a reusable shape. Use `Result`'s generic helpers for one-off conditions that
don't need a named, reusable check:

| Intent | Use |
| --- | --- |
| A reusable, named constraint | `Check.present value` |
| A bare `bool` condition | `Result.requireTrue error condition` |
| An ad-hoc predicate over the value | `value \|> Result.okIf predicate \|> Result.orError error` |
| Extract an inner value | `Result.someOr error value` |

```fsharp
type RegistrationError = PasswordRequired

let validatePassword password : Result<unit, RegistrationError> =
    not (System.String.IsNullOrWhiteSpace password)
    |> Result.requireTrue PasswordRequired
```

Use `Result` helpers when success exposes an inner value or a deliberately different success shape than the source —
`Result.someOr` checks that an option is `Some` and returns the unwrapped value, for example.

## Flow Bind Sites

Outside `flow {}`, keep pure code in `Result` with `Check.*` calls, `Result.orError`/`Result.mapError`, or the
extracting helpers. Inside `flow {}`, use `Bind.error` only when a source needs an error assigned immediately before
binding.

```fsharp
type LoginError = MissingPassword

let login password =
    flow {
        do!
            password
            |> Check.present
            |> Result.orError MissingPassword
            |> Result.map ignore

        return ()
    }
```

## Going Deeper: The Full Check Surface

Everything above uses the deduplicated root names. The full picture underneath:

### Type-Specific Submodules

`Check.String`, `Check.Number`, `Check.Seq`, `Check.Option`, `Check.ValueOption`, `Check.Nullable`, and `Check.Result`
hold the type-specific implementations. Root names like `Check.minLength` are thin forwarders into `Check.String.minLength`
— one implementation, two entry points. Reach for the qualified form directly when a name would otherwise collide:

| Check | Extract (a different shape, lives elsewhere) |
| --- | --- |
| `Check.Option.some` | `Result.someOr` |
| `Check.ValueOption.some` | `Result.valueSomeOr` |
| `Check.Nullable.hasValue` | `Result.nullableOr` |
| `Check.Result.ok` | `Result.okOr` |
| `Check.Result.error` | `Result.errorOr` |
| `Check.Seq.notEmpty` | `Result.headOr` (first item) |
| `Check.single` | `Refine.exactlyOne` (extracts the element) |
| `Check.atMostOne` | `Refine.atMostOne` (extracts the element) |

Cardinality — "this collection has exactly one item" — is a collection-level structural fact, not a value-level
constraint. `Check.single`/`Check.atMostOne` prove the fact and keep the sequence; extracting the element itself
isn't something a `Check` can do (a `Check` always returns the same type it received), so that lives in `Refine`
instead, reusing `CheckFailed` rather than a separate error type:

```fsharp
ids |> Check.single      // proves the fact, keeps the sequence
ids |> Refine.exactlyOne // extracts the single element
```

### The Type-Directed Presence Facade

`Check.present`, `Check.empty`, and `Check.notEmpty` dispatch across `string`, `option`, `voption`, `Nullable<'value>`,
and sequence-shaped values generically — the same three names work regardless of which type you hand them:

```fsharp
Check.present "Ada"           // Result<string, CheckFailure list>
Check.present (Some 1)        // Result<int option, CheckFailure list>
Check.present (ValueSome 1)   // Result<int voption, CheckFailure list>
Check.present (Nullable 1)    // Result<Nullable<int>, CheckFailure list>
```

This is resolved at compile time via F#'s statically resolved type parameters (SRTP) — there's no runtime type test.
Each call site is compiled directly against the matching `Check.String.present`/`Check.Option.present`/etc.
implementation, so there's no reflection and no boxing overhead beyond what the compiler would generate for a direct
call to the type-specific version.

### Composition

`Check.all`, `Check.any`, `Check.not`, and `Check.mapFailure` combine `Check<'value>` values:

```fsharp
let requiredName =
    Check.all [ Check.present; Check.lengthBetween 2 40 ]
```

`Check.all` runs every check and accumulates all failures; `Check.any` stops at the first success and accumulates
failures only if every alternative fails; `Check.not` inverts a check; `Check.mapFailure` transforms the failures a
check produces without changing what it checks.

Use `Predicate.*` helpers instead when a local branch needs a raw `bool` rather than a structured result.
