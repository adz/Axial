# Lifted value concepts

**Status:** accepted and implemented on `feature/lifted-value-concepts`.

Implementation adjustments agreed during review:

- non-option fields are inherently supplied; omission succeeds when `withDefault` supplies a value;
- lifted length dispatch covers concrete string, list, array, and map shapes, not general `seq`;
- concrete shape modules remain available where naming the shape is useful;
- text `present` emits both `minLength: 1` and a non-whitespace pattern; map length emits `minProperties`;
- field blocks expose type-preserving `defaultValue`, `describe`, and `format` operations;
- `[<DeriveSchema>]` supports `Present`, `Supplied`, `Length`, `LengthBetween`, and open `Format` attributes;
  `Min`/`Max` use lifted natural-length language;
- the `Check.Number`/`Check.Ordered` rename and membership lifting remain deferred.

One thesis: name the concept, let shapes implement it. Polymorphic `present`/`blank` was the original intention and
got lost as per-shape checks accumulated; `length` has the same problem in a different disguise. This records the end
state, not a migration.

Concepts covered: **supply**, **content**, **length**, **ordering**, and **membership** (deferred).

## Supply and content

### The problem

Three concepts share one word. `required` means "the input carried a value" in Schema, and "this text has
non-whitespace content" in Check, and both project to the code `"required"`. `CheckFailure.Required` is the failure
for an omitted key, for blank text, and for `Option.none`.

The bundling is not just untidy. It is unrepresentable:

```json
"properties":{"v":{"type":"string"}},"required":["v"]
```

JSON Schema's `required` means the property is present, so a third-party validator accepts `{"v":""}`. Axial rejects
it. The generated document does not describe the enforcing code — the failure this package exists to prevent.

It also costs a distinction the boundary needs: a diagnostic reading `Required` cannot say whether a field was
missing or merely empty, which is exactly what a form redisplay must know.

### Two duals

| level | dual | question | scope |
|---|---|---|---|
| Supply | `supplied` / `omitted` | did the input carry anything at this key? | boundary, before a typed value exists |
| Content | `present` / `blank` | is the value inhabited? | any shape, polymorphic |

The same concept one level up. They compose, and neither implies the other:

```fsharp
field _.Name { constrain supplied }                 // sent; "" is a legitimate value
field _.Name { constraints [ supplied; present ] }  // sent, and inhabited
field _.Tags { constraints [ supplied; present ] }  // identical shape, list instead of text
```

The text exception disappears — not by deleting the blank check, but because `present` applies uniformly to every
shape and is opted into per field. Text stops being special because nothing is special.

### `present` per shape

| shape | present | blank |
|---|---|---|
| text | has non-whitespace | null, `""`, `"   "` |
| list / array | length > 0 | length = 0 |
| map | length > 0 | length = 0 |
| option / voption | `Some` | `None` |
| `Nullable` | `HasValue` | null |

One constraint and one metadata case. The emitter renders it per shape: `minLength: 1` for text, `minItems: 1` for
arrays. The JSON Schema stops lying without introducing a second concept.

`present` on a collection ignores whether the items are inhabited — `[""; ""]` is present. Rails agrees; state it
anyway, because it surprises once.

### Failures

| | now | end state |
|---|---|---|
| key was not sent | `Required` | `Omitted` |
| value is not inhabited | `Required` | `Blank` |

### Naming moves

| | now | end state |
|---|---|---|
| Schema constraints | `required` / `optional` | `supplied` / `omittable` |
| Schema metadata | `Presence.Required` / `Optional` | `Supply.Supplied` / `Omittable` |
| Check constraint | `Constraint.required : Constraint<string>` | `Constraint.present`, polymorphic |
| Check metadata | `ConstraintMetadata.Present` | unchanged |
| codes | `"required"` / `"optional"` | `"supplied"` / `"omittable"`, still emitted as `required` in JSON Schema |

`supplied`/`omittable` over `required`/`optional`: `omittable` is the true dual of `omitted`, and `required` is the
word that carried two jobs. JSON Schema keeps `required` because that is the wire's vocabulary for the same idea.

### Deletions

- `Option.present`, `Option.empty`, `Option.notEmpty` — aliases for `some`/`none`, subsumed by lifted `present`
- `Nullable.present`, `Nullable.empty`, `Nullable.notEmpty` — same
- `String.IsAbsent` — an alias for `IsBlank`; absence belongs to supply

### What stays, subordinate

Per-shape checks remain where the shape itself is the point rather than inhabitation:

- `Option.some`, `Nullable.hasValue`, `Result.ok`
- `String.email`, `matches`, `numeric`, `alphaNumeric` — text-only formats
- `Seq.noDuplicates` — genuinely collection-only

`String.notEmpty` and `Seq.notEmpty` do **not** stay. Both mean length >= 1, so lifted `length` subsumes them, and
`minLength 1` says it in one vocabulary. This is where the two lifts reinforce: `present` covers inhabitation,
`minLength 1` covers the length-only reading that `blank` deliberately blurs for text.

Already consistent and unchanged: `NonBlankString` names blankness, `NonEmptyList` and `NonEmptyArray` name length.

### Consequences

`supplied` alone stops rejecting blank text. A field that must be inhabited says so with `present`, which is why the
pair has to land together rather than in sequence.

Roughly nine tests in `Axial.Schema.Tests` assert blank-means-required and would move to `[ supplied; present ]`.
Touches `Axial.Check` (constraint, failures, aliases), `Axial.Schema` (constraints, metadata, parse and check paths),
and the JSON Schema emitter.

### Rejected along the way

- **`requiredAllowingBlank`** — a flag wearing a name, subtracting from a bundle instead of unbundling it.
- **Moving the conflation into the `Data` constructors** — Axial's constructors partition by *shape* (flat name/value,
  nested configuration, JSON, CLI), not by wire capability. `ofNameValues` cannot know whether its caller could
  distinguish blank from absent, so it cannot own the decision. ASP.NET can do this in its form value provider only
  because it has a form-specific binder; Axial has no such layer.
- **Dropping type-directed `present`** — the polymorphism is the point, not an accident to be tidied away.


## Length

`length` and `count` are the same concept under two names. The expectation unions are identical, case for case:

```fsharp
type CheckLengthExpectation =        type CheckCountExpectation =
    | MinimumLength of int               | MinimumCount of int
    | MaximumLength of int               | MaximumCount of int
    | ExactLength of int                 | ExactCount of int
    | LengthBetween of int * int         | CountBetween of int * int
```

So are the checks (`String.minLength` / `Seq.minCount`), the metadata (`MinLength` / `MinCount`), and the failures
(`InvalidLength` / `InvalidCount`).

`length` is the lifted name, not `size`: F# already uses it universally — `String.length`, `List.length`,
`Array.length`, `Seq.length`.

```fsharp
constrain (minLength 2)   // >=2 characters, or >=2 items — one word, shape dispatches
```

| shape | length |
|---|---|
| text | character count |
| list / array | element count |
| map | entry count |

Emits `minLength`/`maxLength` for text and `minItems`/`maxItems` for arrays, the same per-shape mechanism `present`
uses.

### Naming moves

| | now | end state |
|---|---|---|
| checks | `Seq.count` / `minCount` / `maxCount` / `countBetween` | `length` / `minLength` / `maxLength` / `lengthBetween` |
| metadata | `Count` / `MinCount` / `MaxCount` / `CountBetween` | `Length` / `MinLength` / `MaxLength` / `LengthBetween` |
| expectation | `CheckCountExpectation` | deleted; `CheckLengthExpectation` covers both |
| failure | `InvalidCount` | deleted; `InvalidLength` covers both |

### Subsumed

`Seq.single`, `atMostOne`, `atLeastOne`, and `moreThanOne` are length predicates in disguise — length `= 1`, `<= 1`,
`>= 1`, `> 1`. They collapse into the lifted vocabulary.

### Length is not blankness

For collections they coincide: blank means length 0. For text they do not — `"   "` has length 3 and is blank. The two
concepts stay independent, and text is the shape that shows why.

## Ordering

Already lifted, only misnamed. `Number.between` and friends are `inline` and generic over comparables, so they
already work on `DateOnly`, `DateTimeOffset`, and strings. Schema calls this family **ordered**
(`ConstraintCheck.ordered`, `SchemaCheck.ordered`); Check calls it **Number**. The seam is a naming inconsistency,
not a semantic one.

`Check.Number.between` -> `Check.Ordered.between`, with `positive`, `negative`, `nonNegative`, and `nonPositive`
staying behind in `Number` because they are genuinely numeric.

Cheapest change here: a rename, no behaviour.

## Membership — deferred

`ConstraintMetadata.OneOf of choices: string list` and `String.oneOf` are string-bound, but "value is one of a closed
set" applies to any equatable — enums, ints, dates. Lifting needs the metadata to carry non-string operands, which
`ConstraintArgument` already supports.

Deferred: strings are the overwhelming case, so the metadata churn costs more than it returns.

## Not liftable

`email`, `matches`, `numeric`, and `alphaNumeric` are genuinely text-only. `noDuplicates` / `distinct` is genuinely
collection-only. Leave them per-shape.
