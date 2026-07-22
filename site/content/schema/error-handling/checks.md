---
weight: 10
title: Check
description: Reusable, named structural checks that attach to Result.
type: docs
---


Open the Check DSL in a module that checks several values:

```fsharp
open Axial.ErrorHandling.CheckDSL

let requiredName : Check<string> = Check.all [ present; minLength 2 ]
let adultAge : Check<int> = atLeast 18

let checkName value = value |> requiredName |> orError NameInvalid
```

A `Check<'value>` is a function from a value to itself-or-a-failure:

```fsharp
type Check<'value> = 'value -> Result<'value, CheckFailure list>
```

A `Check` is useful when the same rule is needed in several places. It keeps the original value when the rule passes
and returns a `CheckFailure` value, rather than a loose string, when the rule fails.

## Use the DSL for check modules

The presence functions work with strings, options, value options, nullable values, lists, and arrays:

```fsharp
present "Ada"           // Result<string, CheckFailure list>
present (Some 1)        // Result<int option, CheckFailure list>
present (Nullable 1)    // Result<Nullable<int>, CheckFailure list>
present [ 1; 2 ]        // Result<int list, CheckFailure list>
```

F# chooses the right function from the value's type. If it cannot work out the type, add a type annotation or use a
specific name such as `Check.String.present`.

The ErrorHandling introduction has the [full DSL list](./). It includes `orError` and `mapError`, so a complete check
pipeline can use the same short style.

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

The failure can be rendered with `CheckFailure.describe` or `describeAll`, matched in code, or changed into a schema
error. `CheckFailureResources` supplies translated messages when the default English text is not suitable.

## Attach a Domain Error

When the caller needs an application error, use `orError` to replace the check details or `mapError` to carry those
details into the new error:

```fsharp
type SignUpError =
    | NameRequired
    | AgeInvalid

let requireAdult age : Result<int, SignUpError> =
    age
    |> atLeast 18
    |> orError AgeInvalid

let requireName name : Result<string, SignUpError> =
    name |> present |> orError NameRequired
```

For example, `mapError` can keep the complete failure list:

```fsharp
type OrderError = InvalidQuantity of CheckFailure list

let quantity value : Result<int, OrderError> =
    value
    |> greaterThan 0
    |> mapError InvalidQuantity
```

## Check Is Not Result

Use `Check` for a rule you want to name and reuse. A `Result` helper fits better when a condition only matters at one
call site, or when success changes the shape of the value.

- `Check` handles rules such as “this string is present” or “this number is at least 18.” It returns the same value
  when the rule passes.
- `Result.requireTrue` and `Result.okIf` handle one-off conditions. Helpers such as `Result.someOr` take a value out
  of another shape.

Use `Result.requireTrue` for a condition used in one place:

```fsharp
type RegistrationError = PasswordRequired

let validatePassword password : Result<unit, RegistrationError> =
    not (System.String.IsNullOrWhiteSpace password)
    |> Result.requireTrue PasswordRequired
```

Use `Result.someOr` when success takes the value out of an option:

```fsharp
let user : Result<User, LoginError> =
    tryFindUser username |> Result.someOr UserNotFound
```

If the same condition appears more than once, give it a name as a `Check`. If a `bool` is used directly by `if` or
`match`, see [Predicates](./predicates/).

A check followed by `orError` or `mapError` is an ordinary `Result`. It works directly in `result {}` and in
[`flow {}`]({{< relref "/flow/" >}}).

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
let requiredName : Check<string> =
    Check.all [ present; lengthBetween 2 40 ]
```

`Check.all` runs every check and accumulates all failures; `Check.any` stops at the first success and accumulates
failures only if every alternative fails; `Check.not` inverts a check; `Check.mapFailure` transforms the failures a
check produces without changing what it checks.

Use [`Predicate`](./predicates/) instead when a local branch needs a raw `bool` rather than a structured result.
