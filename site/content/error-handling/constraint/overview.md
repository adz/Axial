---
weight: 20
title: Using constraints
description: Naming rules, composing them, reading violations, and keeping the value.
type: docs
---


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

`Constraint.present` and the size family resolve across text, lists, arrays, and maps by the type they are used at.
Applied where that type is already known — inside a rule like the one above, or to a schema — they need nothing
extra. On a standalone binding the annotation is the only type information available, so it is what tells the
compiler which shape you meant.

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
    Constraint.any (Constraint.equalTo -1) [ Constraint.positive ]
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

### What "blank" means

`present` means inhabited according to the shape: `None`, `ValueNone`, an empty `Nullable`, a null or empty
collection or map, and null or whitespace-only text are all blank. `minLength 1` is a literal size, so a single
space satisfies it while `present` does not.

For text specifically, blank means **whitespace as .NET defines it, plus U+FEFF** (the byte-order mark). That last
character is deliberate, and it is what lets the rule be published at all.

A JSON Schema validator decides whitespace by ECMA-262's `\s`, which is not quite .NET's set. The two used to
disagree in both directions, and one of those directions is genuinely harmful: where a validator treats a character
as whitespace and Axial does not, an exported schema rejects a payload the library would have accepted — and the
library never sees it to explain why. U+FEFF was the whole of that direction, since .NET Core dropped it from
`Char.IsWhiteSpace` while ECMA-262 keeps it. Treating it as blank removes the problem.

What remains is the harmless direction: a few characters, U+0085 among them, are blank here but ordinary to a
validator. Such a value passes the wire check and then fails at Axial with a proper diagnostic, which is what you
want anyway. Because of that, `present` on text exports as `pattern: "\\S"` and `trimmed` exports too, where both
were previously runtime-only.

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

To render in another language see [Localization](../localization/). Every built-in failure carries a catalogue key
and named arguments, and `Violation.renderWith` runs the whole path through one lookup.

## Check or extract

Constraints preserve shape. `check` answers whether a value satisfies a rule, `guard` hands the same value back so a
pipeline can continue, and `test` gives a `bool` when a local branch wants nothing structured:

```fsharp
"Ada" |> Constraint.check name   // Result<unit, Violation>
"Ada" |> Constraint.guard name   // Result<string, Violation>  -- the value, unchanged
"Ada" |> Constraint.test name    // bool
```

None of them changes the *type* of the value. Extraction does, and lives on Result instead:

| Prove a fact | Extract a value |
| --- | --- |
| `Constraint.present : Constraint<'a option>` | `Result.someOr` |
| `Constraint.present : Constraint<'a voption>` | `Result.valueSomeOr` |
| `Constraint.present : Constraint<'a Nullable>` | `Result.nullableOr` |
| `Constraint.present : Constraint<'a list>` | `Result.headOr` |

The split is what makes one constraint usable by many interpreters. A schema lowers a rule, a generator satisfies it,
a document publishes it — and all three need the rule to be a claim *about* a value rather than a transformation of
one. `someOr` says in its own type that it produces an `'a` from an `'a option`; folding that into `present` would
give a rule whose meaning depended on what the caller wanted back, and nothing downstream could read it.
