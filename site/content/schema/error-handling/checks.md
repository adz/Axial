---
weight: 10
title: Check
description: Reusable, named structural checks that attach to Result.
type: docs
---


A `Check<'value>` is a function from a value to itself-or-a-failure:

```fsharp
type Check<'value> = 'value -> Result<'value, CheckFailure list>
```

That's the whole shape. What makes it worth reaching for instead of writing the same `if`/`Error` by hand is that a
`Check` is **named** and **reusable** — `Check.present`, `Check.atLeast 18`, `Check.email` are each written once and
called from everywhere that fact matters — and its failure side is **structured**, not a bare `unit` or `string`.

## Reach For The Root Names First

The common facts live directly on `Check`, and the three presence names are type-directed: `Check.present`,
`Check.empty`, and `Check.notEmpty` accept a `string`, `option`, `voption`, `Nullable<'value>`, `list`, or `array`,
and each call site resolves to the right rule for that type:

```fsharp
Check.present "Ada"           // Result<string, CheckFailure list>       — non-blank
Check.present (Some 1)        // Result<int option, CheckFailure list>   — is Some
Check.present (Nullable 1)    // Result<Nullable<int>, CheckFailure list> — has a value
Check.present [ 1; 2 ]        // Result<int list, CheckFailure list>     — non-empty
```

The dispatch is F#'s statically resolved type parameters (SRTP), resolved entirely at compile time — no runtime type
test, no reflection; each call compiles directly against the matching type-specific implementation. The result type
comes from context rather than the input, so a surrounding annotation (a function's return type, a binding's type)
must pin it; in the middle of an inference chain with nothing pinning the result, use the type-specific form
(`Check.String.present`) instead.

The rest of the root catalog is ordinary named functions: `Check.atLeast`, `Check.email`, `Check.minLength`,
`Check.maxCount`, `Check.matches`, `Check.oneOf`, and so on. Start here; most call sites never need anything else.

## Dot Into A Type's Module To Browse

When you know the type and want to see what can be checked about it, the per-type modules are the catalog:
`Check.String`, `Check.Number`, `Check.Seq`, `Check.Option`, `Check.ValueOption`, `Check.Nullable`, and
`Check.Result`. Typing `Check.String.` lists every string check in completions; `Check.Seq.` lists every collection
check. Root names like `Check.minLength` are thin forwarders into these modules — one implementation, two entry
points — so the qualified form is never a different behavior, just a browsable one.

The qualified form is also the disambiguator when a name would otherwise collide, and it states the rule precisely
where a call site reads better with the container named — `Check.Option.some ticket.Assignee` says exactly what is
being proven.

## The `CheckFailure` Type

Every `Check` that fails produces one or more [`CheckFailure`]({{< relref "/schema/reference/error-handling/check/t-errorhandling-checkfailure.md" >}})
values — a closed set of describable reasons, not free-form text:

```fsharp
type CheckFailure =
    | Required                                                  // a required value was missing
    | InvalidFormat of expected: string                         // didn't match an expected format (e.g. email)
    | InvalidLength of expectation: CheckLengthExpectation * actualLength: int option
    | OutOfRange of expectation: CheckRangeExpectation * actual: string option
    | InvalidCount of expectation: CheckCountExpectation * actualCount: int option
    | NotOneOf of choices: string
    | Duplicate
    | Custom of code: string
```

Because the reason is a real value instead of a string, it can be rendered (`CheckFailure.describe`/`describeAll`,
with a swappable [`CheckFailureResources`]({{< relref "/schema/reference/error-handling/check/" >}}) for localization), pattern-matched
on, or lowered into a `SchemaError` when the check runs as part of schema input parsing. Most application code just
maps the whole list to a domain error, which is what the rest of this page shows — but the structure is there when
you need more than "it failed."

## The CheckDSL

Open `Axial.ErrorHandling.CheckDSL` inside a module that runs several checks, and write them bare:

```fsharp
module SignupChecks =
    open Axial.ErrorHandling.CheckDSL

    let validateAge : Check<int> = atLeast 13
    let validateEmail : Check<string> = Check.all [ present; email ]
```

`present`, `atLeast`, `email`, `minLength`, `maxLength`, `lengthBetween`, `exactLength`, `matches`, `oneOf`,
`greaterThan`, `lessThan`, `atMost`, `positive`, `nonNegative`, `negative`, `nonPositive`, `minCount`, `maxCount`,
`countBetween`, `equalTo`, `notEqualTo`, `empty`, `notEmpty`, and `mapFailure` are all here unqualified. `Check.all`,
`Check.any`, and `Check.``not``` stay qualified even with the DSL open — they, along with `contains`, `distinct`,
`length`, and `between`, shadow FSharp.Core names, so the DSL deliberately leaves them off its surface.

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

## Check Is Not Result

`Check` and `Result`'s generic helpers are not two ways to do the same thing — they answer different questions, and
neither is a drop-in substitute for the other:

- `Check` proves a **named, reusable structural fact about a value** — "this string is present," "this number is at
  least 18" — and hands the same value back unchanged on success. It exists so the fact can be written once, given a
  name, and reused across every place that needs it.
- `Result`'s generic helpers (`Result.requireTrue`, `Result.okIf`, `Result.someOr`, and friends) exist for everything
  a `Check` doesn't cover: one-off conditions that don't deserve a name, extracting an inner value from an `option`
  or `Nullable`, or shaping a success value differently from its source.

Reach for `Result.requireTrue` when the condition is bespoke to one call site, not a reusable fact:

```fsharp
type RegistrationError = PasswordRequired

let validatePassword password : Result<unit, RegistrationError> =
    not (System.String.IsNullOrWhiteSpace password)
    |> Result.requireTrue PasswordRequired
```

Reach for `Result.someOr` when success means unwrapping a different shape than the input, not merely re-affirming it:

```fsharp
let user : Result<User, LoginError> =
    tryFindUser username |> Result.someOr UserNotFound
```

If you find yourself writing the same `Result.requireTrue`/`Result.okIf` condition in more than one place, that's the
signal to promote it to a `Check` instead, so it gets a name and composes with `Check.all`/`Check.any`.

For a raw `bool` that never needs to become a `Result` at all — an `if` guard, a `match` condition — see
[Predicates](./predicates/); that's a third, separate concern from both of these.

A `Check` piped into `Result.orError`/`Result.mapError` is already a plain `Result`, so it binds directly inside
`flow {}` (a separate package — see [Flow]({{< relref "/flow/" >}})) the same way it does in a `result {}`; no
adapter is needed either direction.

## Check Or Extract

A `Check` always hands back the same type it received. When success should instead *unwrap* a value — the inner
value of an option, the head of a sequence — that's extraction, and it lives elsewhere:

| Check (proves, keeps the shape) | Extract (unwraps) |
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
lives in `Refine` instead, reusing `CheckFailed` rather than a separate error type:

```fsharp
ids |> Check.single      // proves the fact, keeps the sequence
ids |> Refine.exactlyOne // extracts the single element
```

## Composition

`Check.all`, `Check.any`, `Check.not`, and `Check.mapFailure` combine `Check<'value>` values:

```fsharp
let requiredName =
    Check.all [ Check.present; Check.lengthBetween 2 40 ]
```

`Check.all` runs every check and accumulates all failures; `Check.any` stops at the first success and accumulates
failures only if every alternative fails; `Check.not` inverts a check; `Check.mapFailure` transforms the failures a
check produces without changing what it checks.

Use [`Predicate`](./predicates/) instead when a local branch needs a raw `bool` rather than a structured result.
