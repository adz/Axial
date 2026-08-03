---
weight: 30
title: Interpreted and opaque
type: docs
description: What makes a rule inspectable, what an escape hatch costs, and where the honesty boundary sits.
---

# Interpreted and opaque

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
own violation, for when the failure deserves a structured reason. Its callback is exactly the shape of
`Constraint.check` applied to a rule, so the usual way to supply one is to reuse a built-in rather than build a
violation by hand:

```fsharp
let currency : Constraint<string> =
    Constraint.customWith "must be a supported currency" (Constraint.check (Constraint.oneOf supported))
```

The enclosing description stays opaque, so reporting an interpreted leaf this way makes no false portable claim.
Construct a `Violation` directly only when no built-in states what you mean.

Descriptions are required and must be non-blank, because they are the only thing a renderer has to work with. They
are also, by default, untranslatable — see [Localization](./localization/) for `customLocalized`, which lets you
attach your own catalogue key.

`Constraint.contramap` is opaque for the same reason: an arbitrary projection changes the proposition in a way no
description can express. The inner description is retained beneath the boundary so documentation stays readable, and
an opaque child never erases its portable siblings — the rest of the expression stays inspectable.

## Negation

Several rules are already negative, and each is a first-class interpreted primitive rather than a complement applied
to something else:

| Rule | Negates | Stays interpreted |
| --- | --- | --- |
| `Constraint.blank` | `present` | yes — exact complements for every supported shape |
| `Constraint.notEqualTo` | `equalTo` | yes |
| `Constraint.noneOf` | `oneOf` | yes — exports as a refused `enum` |
| `Constraint.notContains` | `contains` | yes — exports as a refused `contains` |
| `Constraint.notWith` | anything | **no** — opaque, and requires prose |

Reach for the specific one whenever it says what you mean. A reserved-name rule is `noneOf`, not a negated `oneOf`:

```fsharp
let handle : Constraint<string> =
    Constraint.noneOf [ "admin"; "root" ]
```

Both run the same predicate, but only the primitive is inspectable — it lowers to JSON Schema, generates, and
documents itself, where a negated rule can do none of those.

### Why there is no general `not`

`Constraint.notWith` is the only general negation, and it requires prose because its failure has no reason to derive:

```fsharp
Constraint.notWith "must not be a supported ISBN prefix" isbnPrefix
```

The catalogue could not offer an interpreted `not` honestly. Format, uniqueness, and the numeric properties have no
complement inside their family. Float comparisons are not complementable under `NaN`, where both `x > y` and
`x <= y` are false. A cardinality complement would need bounds the catalogue rejects, such as a maximum of -1. An
operation that is sometimes interpreted, sometimes needs prose, and sometimes cannot be constructed at all is worse
than one that is honestly opaque — so where a complement *is* expressible, it is published under its own name in the
table above instead.

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

Because it never reaches a violation, `describe` takes no catalogue key and is not localized: it is documentation
about a rule rather than a message to a user, so translating it is a matter of generating the document per locale.
Localization applies to failures, and is covered [there](./localization/).

## What export does with each tier

Interpreters divide by what they *claim*:

- **Admission and value generation fail closed.** `Schema.parse`, `Schema.check`, refinement creation, and SchemaGen
  either execute every applicable constraint or report the one they cannot support, with its path.
- **Trusted structural codecs make no constraint claim** at all. They enforce wire shape and construction, and stay
  outside constraint interpretation entirely.
- **Documentation and export degrade honestly.** JSON Schema emits every keyword the target really enforces and
  retains the rest as readable prose plus `x-axial-runtime-constraints` entries, so a published document never implies
  a rule is enforced when it is not.

That last rule has teeth, and lowering has three fidelities rather than two:

| Fidelity | Meaning | Example |
| --- | --- | --- |
| Exact | the keyword means what the runtime rule means | `maxLength`, `enum`, `Constraint.email` |
| Weakened | the keyword never rejects what the runtime accepts; the exact rule is also retained as runtime metadata | `Constraint.present`, `Constraint.trimmed` |
| Runtime-only | no keyword is sound; the rule is retained as metadata alone | an authored `Constraint.pattern` |

`Constraint.present` and `Constraint.trimmed` are weakened rather than exact because [blankness](../overview/#what-blank-means)
covers a few characters a validator does not. That direction is safe; the reverse would not be, which is why the
blank set is defined to make it impossible.

An authored `Constraint.pattern` stays runtime-only because the .NET regex dialect is not ECMA-262 — `\d`, for one,
matches any Unicode decimal digit on .NET and only `[0-9]` under ECMA-262, so publishing an authored pattern could
silently change what it means. Axial's own patterns are written in the common subset and lower exactly:
`Constraint.email` emits its exact runtime pattern, and `Constraint.numeric` is defined as ASCII digits for
precisely this reason.

Keywords that JSON Schema allows only once per node are merged rather than duplicated: several excluding rules
become one `not: {anyOf: [...]}`, and several pattern-shaped rules become one `allOf`.

Finally, `SchemaFormat.email` is a separate annotation that lowers to `format: "email"`. It makes no validation
claim of its own, and declaring it alongside `Constraint.email` emits both.
