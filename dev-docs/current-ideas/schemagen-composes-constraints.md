# SchemaGen must compose constraints, not pick one and ignore the rest

**Status:** proposed
**Depends on:** the constraint-unification branch, whose `ConstraintAtom` vocabulary the fix builds on. The
defect itself predates it and is reproducible on `main`.

## Problem

`SchemaGen` promises structured data that satisfies a schema's constraints. For a field carrying more than one
constraint it can return data that violates them, and reports success when it does.

Measured by generating 40 samples per schema and parsing each back through the schema it came from:

```
minLength 5 + maxLength 10        ok (40/40)
present + minLength 3             ok (40/40)
email alone                       ok (40/40)
atLeast 18 + atMost 65            ok (40/40)
atLeast 18 + atMost 65 + mult 10  ok (40/40)

email   + maxLength 10            40/40 invalid   -> "ada@example.com"  (15 characters)
oneOf   + minLength 10            40/40 invalid   -> "beta"
equalTo + minLength 10            40/40 invalid   -> "exact"
equalTo + email                   40/40 invalid   -> "exact"
```

Numeric and length combinations compose correctly, because the numeric generators intersect their bounds and
then step by the divisor. The text generator does not: rules that *pick* a value return early with their own
choice and never consult the other atoms.

Two distinct failures are mixed in there:

1. **A wrong answer on a satisfiable schema.** `email + maxLength 10` is satisfiable — `a@b.co` is six
   characters. The generator emits a fixed fifteen-character address anyway.
2. **No inconsistency detection.** `equalTo "exact" + minLength 10` is unsatisfiable. Nothing correct exists,
   but the generator emits a value and reports success rather than saying the combination cannot be met.

## Root cause

Support is decided per rule, in isolation, and the first rule that can produce a value wins. `email`, `oneOf`,
and `equalTo` each answer "I can generate that" truthfully about themselves and falsely about the field, because
none of them looks at what else is attached. The generator never checks its own output against the rules it was
given.

## Why it matters

The loud failure is the harmless one. `SchemaGen.raw` handing back invalid data fails an obvious assertion.

`SchemaGen.model` is the dangerous path:

```fsharp
|> Gen.map (Schema.parse schema)
|> Gen.filter Result.isOk
```

Invalid data is silently discarded. A broken generator therefore does not fail — it narrows the population the
property runs over, or exhausts the filter. Tests keep passing while covering less than their author believes,
and nothing in the output says so.

That matters because generating *valid domain values* is what SchemaGen is actually for. Asserting that
schema-generated data parses through the same schema is close to circular; its worth is only as a differential
check between two interpreters of one declaration. The real use is supplying realistic inputs to properties
about everything downstream — codec round-trips, HTTP handlers, business logic — without hand-written fixtures
that drift as the schema changes. A generator that quietly narrows its population undermines exactly that.

## Fix

Compose the atoms into one generator instead of letting the first applicable rule win.

**Finite rules become a candidate set, filtered exactly at construction.** `equalTo v` is `{v}`; `oneOf [a; b; c]`
is `{a; b; c}`. Filter that set once against the remaining atoms while *building* the generator:

- survivors remain → `Gen.elements` over them, with no retry at sample time;
- none survive → `Error`, naming the field and the rules that cannot hold together.

This is what turns case 2 from a silent contradiction into a reported one, and it reports at construction:
`SchemaGen.raw` already returns `Result`, so an unsatisfiable field fails before a single value is drawn.

**Infinite rules are parameterised rather than filtered.** `email` currently returns a fixed list. It should take
the length bounds already computed from the cardinality atoms and construct `local@domain` to fit, the same way
the plain text generator already picks a length with `Gen.choose (minimum, maximum)`. Construction, not
rejection.

**Filter with the real semantics, not a copy of them.** Deciding whether a candidate survives needs a predicate.
Hand-writing one would restate code-point length counting, the email pattern, and the rest — the exact drift the
constraint design exists to prevent. Rebuild a live constraint from the atom instead (`CardinalityAtom (Minimum n)`
→ `Constraint.minLength n`; the algebra is closed, so the mapping is total) and use `Constraint.test`. Real
semantics, no duplication.

### Why not generate-and-filter

Rejection sampling is the obvious alternative and is wrong here. For `email + maxLength 10` the acceptance rate
of a randomly drawn email is approximately zero, so the generator would not be slow — it would hang or exhaust.
Rejection only works when the constraint is loose, which is precisely the case that already works today.

## Scope

In scope: the text generator's composition, the atom-to-constraint rebuild, and construction-time
unsatisfiability reporting.

Out of scope, unless they fall out for free: ordering constraints on text (`atLeast "m"` is lexicographic, not a
length — the bound itself is a valid sample, but it must still satisfy the other atoms), and collection-level
`contains`/`distinct`, which need collection-aware construction.

`Axial.Schema.Testing` is a test-only, non-packable assembly. Nothing here reaches published packages.

## Acceptance

- Every combination in the table above either generates data that parses through its own schema, or returns
  `Error` naming the field — no third outcome.
- Unsatisfiable combinations fail when the generator is built, not when it is sampled.
- No retry loop anywhere in the generator.
- A regression test covers both directions: a satisfiable combination generates, and an unsatisfiable one is
  reported.
- The candidate filter goes through `Constraint.test`, so no constraint semantics are restated in SchemaGen.
