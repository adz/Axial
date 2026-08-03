---
weight: 20
title: Interpreted and opaque
type: docs
description: What makes a rule inspectable, what an escape hatch costs, and where the honesty boundary sits.
---


Constraints come in two tiers. The difference is not a quality judgement; it is whether anything other than the
runtime can understand the rule.

## Interpreted constraints

The built-in catalogue is a closed algebra. Each constructor builds exactly one `ConstraintAtom` and puts that same
value in both its description and any violation it produces, so a primitive's identity and its failure cannot drift
apart.

```fsharp
let nameLength : Constraint<string> = Constraint.maxLength 80

(Constraint.inspect nameLength).Expression
// Atom (CardinalityAtom (Cardinality.Maximum 80))

Constraint.check nameLength (String.replicate 81 "a")
// Error (Atomic (Expected (CardinalityAtom (Cardinality.Maximum 80), Some (Integer 81L))))
```

Because the description is data with a known meaning, other tools can read it: JSON Schema lowers it, SchemaGen
generates values that satisfy it, documentation describes it, and a future solver can reason about it.

You reach this tier by composing built-ins and naming the composition — which covers most real domain invariants.
There is no registration API for user-defined interpreted primitives, and no authored string or argument can claim
inspectable logic. That restriction is what makes the tier trustworthy: a name proves nothing, and an annotation that
nobody checks against the predicate it describes will eventually lie.

Atoms are shape-neutral. `Cardinality.Maximum 5` says nothing about text versus lists; the interpreter combines it
with the surrounding schema shape to reach `maxLength`, `maxItems`, or `maxProperties`.

## Opaque constraints

Anything else runs perfectly well and is honestly invisible to export and proof.

```fsharp
let isbn : Constraint<string> =
    Constraint.custom "must be a valid ISBN" isValidIsbn
```

`custom` takes a predicate and reports the supplied prose on failure. `customWith` takes a callback that returns its
own violation, for when the failure deserves a structured reason:

```fsharp
let currency : Constraint<string> =
    Constraint.customWith "must be a supported currency" (fun code ->
        if supported.Contains code then Ok ()
        else Error (Atomic (Expected (MembershipAtom (OneOf choices), ConstraintValue.tryCreate code))))
```

Descriptions are required and must be non-blank, because they are the only thing a renderer has to work with.

`Constraint.contramap` is opaque for the same reason: an arbitrary projection changes the proposition in a way no
description can express. The inner description is retained beneath the boundary so documentation stays readable, and
an opaque child never erases its portable siblings — the rest of the expression stays inspectable.

## Negation is opaque, deliberately

`Constraint.notWith` is the only negation, and it requires prose:

```fsharp
Constraint.notWith "must not be a reserved name" (Constraint.oneOf [ "admin"; "root" ])
```

There is no interpreted `not`, because there is no honest general complement to derive a reason from. Membership,
format, uniqueness, and numeric families have no complement inside their family. Float comparisons are not
complementable under `NaN`, where both `x > y` and `x <= y` are false. A cardinality complement would need bounds the
catalogue rejects, such as a maximum of -1. An operation that is sometimes interpreted, sometimes needs prose, and
sometimes cannot be constructed at all is worse than one that is honestly opaque.

## Unsupported operands

An interpreted constructor can receive an operand with no portable representation — a `Guid`, a custom comparable
type. The constraint still executes against its typed closure; only its description and its failure decline to name
the operand:

```fsharp
(Constraint.inspect (Constraint.equalTo someGuid)).Expression
// Opaque (UnsupportedOperand (Relation Equal))
```

Nothing is silently approximated and no boxed value escapes through the inspection API.

## Prose that is not a rule

`Constraint.describe` attaches documentation:

```fsharp
Constraint.between 0 10
|> Constraint.describe "Retries before the call is abandoned."
```

This reaches inspection and generated schema prose. It never reaches a violation and never changes what the
constraint means. To change what a failure says, author the rule as `custom` or `customWith` — there is no message
override, because one would let the reported failure diverge from the description that was published.

## What export does with each tier

Interpreters divide by what they *claim*:

- **Admission and value generation fail closed.** `Schema.parse`, `Schema.check`, refinement creation, and SchemaGen
  either execute every applicable constraint or report the one they cannot support, with its path.
- **Trusted structural codecs make no constraint claim** at all. They enforce wire shape and construction, and stay
  outside constraint interpretation entirely.
- **Documentation and export degrade honestly.** JSON Schema emits every keyword the target really enforces and
  retains the rest as readable prose plus `x-axial-runtime-constraints` entries, so a published document never implies
  a rule is enforced when it is not.

That last rule has teeth. `Constraint.present` on text emits `minLength: 1` — a sound weakening — and keeps the
non-blank rule as runtime metadata, because .NET whitespace and ECMA-262 `\s` disagree in *both* directions and no
pattern captures the runtime rule. An authored `Constraint.pattern` stays runtime-only, because the .NET regex dialect
is not ECMA-262. `Constraint.email` lowers to its exact runtime pattern; the separate annotation `SchemaFormat.email`
lowers to `format: "email"`, and declaring both emits both.
