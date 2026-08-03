---
weight: 10
title: Using constraints
description: Naming rules, composing them, reading violations, and keeping the value.
---

# Using constraints

```fsharp
open Axial.Constraint
```

A constraint tests an existing typed value. It never trims, normalizes, or replaces its input.

## Name a reusable rule

```fsharp
let name : Constraint<string> =
    Constraint.all [ Constraint.present; Constraint.lengthBetween 2 40 ]

Constraint.check name "Ada"
// Ok ()
```

The type annotation is not decoration. `Constraint.present` and the size family resolve across text, lists, arrays,
and maps by the type they return, so the binding is what tells the compiler which shape you meant.

## Compose

`Constraint.all` runs every child against the same value in declaration order and accumulates the failures. The empty
list is the satisfied identity.

`Constraint.any` takes a first alternative plus the rest, evaluates left to right, and stops at the first success. It
never throws, because an empty disjunction — which nothing could satisfy and which has no reason to report — cannot be
written.

Use `any` for a valid set with a hole in it, which neither a list of literals nor a range can express. The recurring
case is a sentinel beside a real value:

```fsharp
/// A TTL is either the sentinel -1, meaning "never expire", or a positive number of seconds.
let ttl : Constraint<int> =
    Constraint.any (Constraint.equalTo -1) [ Constraint.atLeast 1 ]
```

That is a *wire-tier* rule. Domain code should still model the union honestly as `Never | After of Duration`; `any`
exists because a fixed protocol encodes that union as one field, and the schema has to admit the encoding before a
constructor can produce the domain value.

## Presence, blankness, and optionality

Three operations read as more similar than they are:

| Operation | Absence | Presence |
| --- | --- | --- |
| `Constraint.present` | rejected | required to satisfy the inner shape |
| `Constraint.blank` | required | rejected |
| `Constraint.optional inner` | permitted | must satisfy `inner` |

So `blank` *requires* absence while `optional` *permits* it. Reaching for `blank` to mean "this field may be empty"
gives a constraint that rejects every real value.

```fsharp
let nickname : Constraint<string option> =
    Constraint.optional (Constraint.lengthBetween 2 40)
```

Whether a property may be *omitted from the input* is a different question again, and belongs to Schema's
[`mustSupply`/`mayOmit`]({{< relref "/schema/" >}}).

`present` means inhabited according to the shape: whitespace-only text is blank, as are null text, a null or empty
collection or map, `None`, `ValueNone`, and an empty `Nullable`. `minLength 1` is a literal size, so a single space
satisfies it.

## Keep the value

```fsharp
let checkedName : Result<string, Violation> =
    "Ada" |> Constraint.guard name
```

`guard` returns the unchanged input after success. `Result.guard` remains the generic adapter for ordinary
unit-returning functions.

Map the whole violation once at the application boundary:

```fsharp
type SignupError = InvalidName of Violation

let validateName value =
    value
    |> Constraint.guard name
    |> Result.mapError InvalidName
```

## Read a violation

A `Violation` answers "why did this value fail its constraint?" It is a diagnostic contract, not an application error
union, and it is plain comparable data — no closure and no constraint description is reachable from one, so it can be
retained, compared, and asserted on long after the constraint that produced it went out of scope.

```fsharp
match Constraint.check name "" with
| Ok () -> ()
| Error violation ->
    Violation.render violation
    // "value must be present; expected a size between 2 and 40, but was 0"
```

Most code stops there. When more is needed, a failure carries the failing constraint's own identity rather than a
string to parse:

```fsharp
let failure = Constraint.check (Constraint.minLength 3: Constraint<string>) "ab"

// Violation.tryExpectation failure = Some (CardinalityAtom (Cardinality.Minimum 3))
// Violation.tryActual failure      = Some (ConstraintValue.Integer 2L)
```

`Violation.children` and `Violation.flatten` traverse groups. Rendering keeps conjunctions and alternatives distinct:
`all` failures join with `; `, `any` failures with `, or `.

Axial never produces an empty or single-child group — one failing child is reported directly rather than wrapped.

## Localize

`Violation.render` is the zero-dependency English default. Real localization projects to structured data and lets an
existing i18n system render it:

```fsharp
match Violation.toMessageTree violation with
| MessageTree.Leaf (MessageLeaf.Localized descriptor) ->
    descriptor.Key        // "constraint.cardinality.minimum"
    descriptor.Arguments  // map [ "minimum", Integer 3L; "actual", Integer 2L ]
| MessageTree.Leaf (MessageLeaf.Verbatim prose) ->
    prose                 // author-supplied text, never localizable
| grouped ->
    // All/Any structure is preserved so a translator controls word order.
    ()
```

Keys are derived mechanically from the atom, so the whole catalogue is enumerable and can be generated as an ICU or
resource template. Prose you supplied to `Constraint.custom` passes through verbatim: inventing a resource key for
your own text would promise a lookup that cannot exist.

## Check or extract

Constraints preserve shape by returning `unit` on success. Extraction changes shape and belongs to Result:

| Prove a fact | Extract a value |
| --- | --- |
| `Constraint.present : Constraint<'a option>` | `Result.someOr` |
| `Constraint.present : Constraint<'a voption>` | `Result.valueSomeOr` |
| `Constraint.present : Constraint<'a Nullable>` | `Result.nullableOr` |
| `Constraint.present : Constraint<'a list>` | `Result.headOr` |

Use `Constraint.test` when a local branch needs a `bool` rather than a structured violation.
