# Axial — Parse / Refine / Schema: implementation brief

**Audience:** an engineer or LLM implementing against the `adz/Axial` repository.
**Status:** design brief, not yet implemented. Nothing committed.
**Breakage:** the maintainer does not care about backward compatibility. Ship the
ideal end state as a breaking major.

**Thesis:** parse and refine are **different operations** and stay distinct at every
layer (type, verbs, CEs, schema DSL, errors). A *refinement* narrows a value while
preserving its representation (`int → PositiveInt`); a *parse* changes representation
(`string → int`, `Data → User`). Only refine needs a first-class type; parse is
functions + codecs. Proof-carrying is opt-in at the schema.

---

## Summary — benefits and costs

Core idea: validation is one pattern — a value plus a proof it passed a checker, the
proof living in a private constructor with the checker as sole door, not in the data.

- **Named refined types** (`PositiveInt`, `BoundedString`), kept distinct and enriched
  with members. *Benefit:* names in errors, methods (no wrap/unwrap), familiar. *Cost:*
  a hand-written type each (earned by the members).
- **`Refined<'proof,'a>` — the low-ceremony way to construct a refined value without a
  full named type.** *Benefit:* proof-carrying values with almost no authoring; sound.
  *Cost:* generic, so reads go through `raw`/accessors — less ergonomic than a named type.
- **One transform type, `Refinement` (repr-preserving); no `Parser` type.** *Benefit:*
  matches the definition of a refinement type; parse stays plain functions/codecs; less
  vocabulary. *Cost:* the asymmetry (only refine has a type) must be understood.
- **Opt-in proof at the schema** (`parse`/`check` stay naked; `RefinedSchema<'m>` returns
  `Refined<'m>`). *Benefit:* the common path pays no unwrap tax. *Cost:* two schema surfaces.
- **Flipped error hierarchy: `Check ⊂ Refine ⊂ Parse`.** *Benefit:* `parse {}` is a real
  umbrella boundary CE (accepts either), errors auto-lift. *Cost:* `Parse` depends on
  `Refined` (no longer a light zero-dep on-ramp).
- **Two tier CEs, `parse {}` and `refine {}` (no `check {}`).** *Benefit over the Result
  CE:* raw-value auto-dispatch + automatic cross-tier error lifting. *Cost:* two builders.
- **`[<DeriveRefinedSchema>]` + `[<SchemaConstructor>]`.** *Benefit:* proof-carrying domain
  records with no boilerplate; compile-time, AOT/Fable-safe. *Cost:* an attribute.
- **Packages split at taught concepts under `Axial.ErrorHandling`, prefix kept.**
  *Benefit:* searchability, on-ramps, cross-language safety. *Cost:* more packages.

Overall: a breaking major with more vocabulary, buying proof-carrying domain types at
the library level, opt-in so nobody pays who doesn't use it.

---

## 1. Decision log (all locked)

| # | Decision | Rationale |
|---|---|---|
| D1 | **Parse ≠ refine, everywhere.** Refinement = `{x:T | P(x)}`, representation-preserving. Parse changes representation. | The accepted definition of a refinement type; a parse is categorically not a refinement. |
| D2 | **One transform type, `Refinement<'raw,'value>` (repr-preserving), in `Axial.Refined`. No `Parser` type.** | Only refine needs a reusable, SRTP-dispatched, schema-consumable type. A parse is a plain function; forcing a symmetric `Parser` type was the source of churn. |
| D3 | **`Refined<'proof,'a>`** (private case, `Axial.Refined`) — low-ceremony way to construct a refined value without a full named type. `Refined<'m>` = `Refined<'m,'m>`. Never double-wrap. | Definition-clean (`{T|P}`, wraps `'a`). |
| D4 | **Wrapper doors:** `Refined.make` (checked entry, bring-your-own-checker) + `Refined.trust` (unchecked entry, greppable). Exit `Refined.raw`/`.Value` (total). | Private case blocks forging. `raw` safe out; `trust` the one dangerous in. No `InternalsVisibleTo`. |
| D5 | **Named primitives stay distinct nominal types, enriched** (re-validating `map`; Bounded* keep bounds + `.Length`/`.Remaining`/`.MinLength`/`.MaxLength`). No abbreviations. | Names in diagnostics; members; no wrap/unwrap. |
| D6 | **Proof is OPT-IN at the schema via the output type.** `Schema.parse`/`check` stay naked (`Result<'m, SchemaErrors>`). No `validate`/`certify`. | No unwrap tax on the common path. |
| D7 | **`RefinedSchema<'m>`** (thin real type via `Schema.refined`) hosts `.Parse`/`.ParseRetainingInput`/`.Check`/`.Update` returning `Refined<'m>`; `.Update` edits the naked inner and re-checks. | Carries the schema so nothing is threaded; the `with` problem solved. |
| D8 | **`Schema.refine` = genuine refinements only; repr-changing fields use `Schema.convert`** (generalized to a fallible construct). Remove `Refinement.parsing`. | The DSL already has `convert` for representation changes; parses were wrongly routed through `refine`. |
| D9 | **FLIP the error hierarchy: `ParseError ⊃ RefinementError ⊃ CheckFailure`.** Dep reorders to `Parse → Refined → Check`. | Makes `parse {}` a real umbrella CE (accepts either); matches "the boundary act is parsing." Parse stops being a zero-dep root (accepted). |
| D10 | **Two tier CEs: `parse {}` (`ParseError`, accepts parse+refine+check) and `refine {}` (`RefinementError`, accepts refine+check). No `check {}`.** | Value over the Result CE = raw auto-dispatch + auto cross-tier lifting. `check {}` adds nothing `Check.all`/Schema don't. |
| D11 | **`[<DeriveSchema>]`→`Schema<'m>`; `[<DeriveRefinedSchema>]`→`RefinedSchema<'m>`.** `[<SchemaConstructor>]` = smart-ctor hook (cross-field invariants, can fail). | Closes the original goal with no hand-written boilerplate; source-gen, AOT/Fable-safe. |
| D12 | **Refined-type contract = an interface (static-abstract member) for owned types AND SRTP for third-party types.** | Owned types self-describe (observable, good errors); foreign types still participate. Verify Fable static-abstract support; fallback = plain interface + SRTP. |
| D13 | **Packages:** `Axial.Parse`, `Axial.Refined`, `Axial.Check`, `Axial.Result` under metapackage `Axial.ErrorHandling`. `Predicate` stays in Check. Keep the `Axial.` prefix. | Package list matches taught tiers; searchability; on-ramps; cross-language collision safety. |

---

## 2. Vocabulary

| Name | Kind | Lives in | Meaning |
|---|---|---|---|
| `Refinement<'raw,'value>` | transform TYPE | `Axial.Refined` | repr-PRESERVING refine: `create : 'raw -> Result<'value, RefinementError>` + `raw : 'value -> 'raw` (total reverse, free from repr-preservation). A partial embedding (prism), **not** an iso. |
| `Refined<'proof,'a>` | result WRAPPER | `Axial.Refined` | value carrying phantom proof `'proof`; low-ceremony refined value. |
| `Refine.*` | verb SURFACE | `Axial.Refined` | narrowing constructors (`Refine.positive`). |
| `Parse.*` | plain functions | `Axial.Parse` | `string -> Result<_, ParseError>`. No `Parser` type. |
| `Check` / `Check.all` | predicates | `Axial.Check` | value predicates → `CheckFailure`. Contains `Predicate`. |
| `Schema<'m>` / `RefinedSchema<'m>` | schema | `Axial.Schema` | naked vs proof-returning schema surfaces. |
| errors | | | `ParseError` ⊃ `RefinementError` ⊃ `CheckFailure` (linear). |

---

## 3. Ground truth + dependency graph

Confirm against the tree before coding.

**Today (pre-change):** `Refinement<'raw,'value>` (`create` + `inspect`) exists in
`Axial.Refined`; it currently double-duties for parses (`Refinement.parsing`,
`RefinementFrom.Refinement(string,int/…)`). `Schema.parse`/`check` return the naked
model; `check`'s docstring says no proof wrapper. `Schema.refine` and `Schema.convert`
both exist (`convert`'s construct is total). `RefineBuilder.Bind` is SRTP-dispatched.
`Derive` = inert attributes read by `schemagen` (source generator, no runtime
reflection). `ValueSchema.refined` requires `construct` + `inspect`: `construct` serves
construction/parse interpreters, `inspect` serves **only** inspection interpreters
(codecs, JSON Schema, UI, docs). Current deps: Check/Data/Result zero-dep; Refined→Check
(Parse is a *module inside* Refined); Schema→Check+Data+Refined.

**Target graph (after the flip, D9):**

```
Check   Data   Result          (zero Axial deps)
  \
 Refined → Check
  \
 Parse → Refined  (→ Check)     (Parse is NOT a root; its ERROR type reaches Refined)
  \
 Schema → Parse + Data          (Refined, Check transitive)
ErrorHandling = metapackage(Check, Refined, Parse, Result)
```

Only `ParseError`'s `RefinementFailed` case makes `Axial.Parse` depend on `Axial.Refined`;
the plain `Parse.*` functions still don't use Refined.

---

## 4. The transform: `Refinement` only (D1, D2)

```fsharp
// Axial.Refined — the ONE reusable transform. Representation-preserving.
[<Sealed>]
type Refinement<'raw, 'value> internal
        (create: 'raw -> Result<'value, RefinementError>,   // forward: partial (may fail)
         raw:    'value -> 'raw)                            // reverse: TOTAL, free (was `inspect`)
module Refinement =
    let define : ('raw -> Result<'value,RefinementError>) -> ('value -> 'raw) -> Refinement<'raw,'value>
    let create : Refinement<'raw,'value> -> 'raw -> Result<'value,RefinementError>
    let raw    : Refinement<'raw,'value> -> 'value -> 'raw
```

There is **no `Parser` type.** A standalone parse is a plain function
(`Parse.int : string -> Result<int, ParseError>`, in `Axial.Parse`). A repr-changing
schema field is a codec (`Schema.convert`, §8).

**Why refinements are the schema's inspectable building block — stated correctly.** A
refinement is a *partial embedding* (a prism), **not** a round-trip: the reverse
(`value → raw`, e.g. `PositiveInt.Value`) is **total and free** because representation is
preserved; the forward is partial (the check can fail). `int → refine → .Value` does
**not** round-trip (invalid ints are rejected). It is the always-available **total
reverse** — not any round-trip property — that lets the inspection interpreters (codecs,
JSON Schema, UI, docs) recover the raw from a refined value. A repr-*changing* parse has
no free reverse, which is why the inspectable building block is a `Refinement`, not a parse.

**Purify:** remove `Refinement.parsing` and the `RefinementFrom.Refinement(string, *)`
instances. Their jobs are already covered — schema primitive decoding by the schema's own
interpreters; parse-inside-a-CE by the CE dispatch (§7). `Refinement` becomes
repr-preserving only.

---

## 5. Errors — the flipped hierarchy (D9)

```fsharp
// Axial.Check
type CheckFailure = ...                                   // base

// Axial.Refined  (depends on Check)
type RefinementError =
    | CheckFailed of CheckFailure list                    // a refinement is a check + construct
    | InvalidStructure of target: string * reason: string
    // NO ParseFailed anymore

// Axial.Parse  (depends on Refined — the flip)
type ParseError =
    | MissingValue of ...
    | InvalidFormat of ...
    | OutOfRange of ...
    | RefinementFailed of RefinementError                 // the umbrella case
```

`Check ⊂ Refine ⊂ Parse`. The dependency direction follows: `Parse → Refined → Check`.

---

## 6. Named primitives (D5) and the wrapper (D3, D4)

Named primitives stay distinct nominal types and gain members — re-validating `map` on
all; `BoundedString`/`BoundedList`/`BoundedArray` keep stored bounds and gain `.Length`,
`.Remaining`, `.MinLength`, `.MaxLength`; keep the `seq`/`.Head`/`.Tail` surface on
collections. No abbreviations over the wrapper.

```fsharp
// Axial.Refined — the wrapper
type Refined<'proof, 'a> = private | Refined of 'a
    member this.Value = let (Refined v) = this in v
module Refined =
    val make  : ('input -> Result<'value,'error>) -> 'input -> Result<Refined<'proof,'value>, 'error>  // checked ENTRY
    val trust : 'a -> Refined<'proof,'a>                                                                // unchecked ENTRY (greppable)
    val raw   : Refined<'proof,'a> -> 'a                                                                // total EXIT (+ .Value)
```

`raw`/`.Value` is the safe exit; `trust` is the one dangerous entry. `Refined<'m>` =
`Refined<'m,'m>` for the schema/record case.

---

## 7. Computation expressions: `parse {}` and `refine {}` (D10)

Two CEs, each with its own error, related by the flipped hierarchy so the higher one
absorbs the lower:

- `refine {}` : `RefinementError` — accepts refine + check steps (check auto-lifts).
- `parse {}` : `ParseError` — accepts parse + refine + check steps (all auto-lift up).

**Their value is measured against the existing Result CE, not against nothing.** Over
`result {}` they add: (1) SRTP **auto-dispatch of raw values** by target type, and (2)
**automatic cross-tier error lifting**.

```fsharp
// refine {} vs result {}
refine {
    let! name = Refine.nonBlankString rawName
    do!  Check.matches emailRegex rawEmail          // CheckFailure auto-lifts
    let! age  = Refine.positiveInt rawAge
    return { Name = name; Email = rawEmail; Age = age }
}
result {
    let! name = Refine.nonBlankString rawName
    do!  Check.matches emailRegex rawEmail |> Result.mapError RefinementError.CheckFailed   // manual
    let! age  = Refine.positiveInt rawAge
    return { Name = name; Email = rawEmail; Age = age }
}

// parse {} (umbrella boundary flow) vs result {}
parse {
    let! raw : int = qtyStr             // string→int by target type (auto-dispatch)
    let! qty = Refine.positiveInt raw   // RefinementError auto-lifts to ParseError
    return qty
}                                        // Result<PositiveInt, ParseError>
result {
    let! raw = Parse.int qtyStr
    let! qty = Refine.positiveInt raw |> Result.mapError ParseError.RefinementFailed        // manual
    return qty
}
```

**No `check {}`.** Its only distinctive over `result {}` would be collect-all vs
short-circuit, but `Check.all [checks]` already collects for pure validation, and
collect-all-while-building-a-value is the Schema layer's job (`SchemaErrors`). Keep the
Check tier function-shaped.

---

## 8. Schema DSL: `refine` vs `convert` (D8)

`Schema.refine` narrows to genuine (repr-preserving) refinements only — keeps its name,
honest. Representation-changing fields use `Schema.convert`, which already exists; its
`construct` is total today — generalize it to a fallible `construct` returning
`ParseError` (or add a fallible sibling) so it hosts parse fields. Remove
`Refinement.parsing`. The field combinator can't be named `Schema.parse` (collides with
the top-level `Schema.parse : Schema -> Data -> Result`; module bindings don't overload) —
use fallible `convert` or `Schema.parseWith` (cf. the existing `mapWith`).

---

## 9. Schema proof mode (D6, D7) and Derive (D11)

`Schema.parse`/`check` stay naked (`Result<'m, SchemaErrors>`). Proof is opt-in via the
output type: a "refined schema" is `Schema<T>` where `T` is proof-carrying (a self-proving
nominal like `PositiveInt`, or `Refined<record>`). `Schema.refined : Schema<'m> ->
RefinedSchema<'m>` promotes; `RefinedSchema<'m>` carries the schema and exposes:

```fsharp
type RefinedSchema<'m> =
    member Parse               : Data -> Result<Refined<'m>, SchemaErrors>
    member ParseRetainingInput : Data -> RetainedParseResult<Refined<'m>>
    member Check               : 'm   -> Result<Refined<'m>, SchemaErrors>
    member Update              : ('m -> 'm) -> Refined<'m> -> Result<Refined<'m>, SchemaErrors>
```

`Update` edits the naked inner model (`'m -> 'm`, so `{ with }` works), re-checks, re-wraps.
`RetainedParseResult` stays transient/form-oriented (keeps verbatim input for redisplay;
`.Value` raises); it composes with proof for free because it's generic.

Derive: `[<DeriveSchema>]`→`Schema<'m>` (naked; DTOs); `[<DeriveRefinedSchema>]`→
`RefinedSchema<'m>` (generated promotion — free, reflection-free, source-gen). `[<SchemaConstructor>]`
is the smart-ctor hook: routes assembly through a static member, normalizes, and can fail
on cross-field invariants (same path `Schema.check` uses for the date-range rule).

---

## 10. The contract: interface + SRTP (D12)

Give the shared refined-type contract (`create`, `.Value`, the `Refinement`) an explicit
**interface with a static-abstract `Refinement` member** for the types Axial owns — so
they're observable/discoverable with good errors — **and** keep **SRTP** resolution for
third-party types the user can't add an interface to. Verify Fable supports static-abstract
interface members; fallback = plain interface for owned types + SRTP for the rest.

---

## 11. Soundness, Fable

No transparent badge on a public record (unsound across `with`). Enforcement = the private
case + `make`/`trust`. Never double-wrap. `Update`/`Check` re-run the whole checker. Wrapper
and named types are plain reference DUs (Fable-fine); no `[<Struct>]`/`[<Measure>]` tricks;
`schemagen` stays compile-time. No `InternalsVisibleTo` anywhere.

## 12. Migration checklist

- [ ] Flip the errors (D9): `RefinementError` drops `ParseFailed`, keeps `CheckFailed`; `ParseError` gains `RefinementFailed`. Reorder deps `Parse → Refined → Check`; move `ParseError` + plain `Parse.*` into `Axial.Parse`; add `Axial.Parse → Axial.Refined`.
- [ ] Purify `Refinement` to repr-preserving only; rename `inspect`→`raw`; delete `Refinement.parsing` + `RefinementFrom(string,*)` instances; keep SRTP for genuine refinements.
- [ ] No `Parser` type. Standalone parses = plain functions.
- [ ] Enrich named primitives; add `Refined<'proof,'a>` + `make`/`trust`/`raw`/`.Value` + `Refined<'m>` alias.
- [ ] Add `parse {}` and `refine {}` CEs (auto-dispatch + auto cross-tier lifting); no `check {}`.
- [ ] Schema: keep `parse`/`check` naked; add `RefinedSchema<'m>` + `Schema.refined` + `.Parse`/`.ParseRetainingInput`/`.Check`/`.Update`; no `validate`/`certify`.
- [ ] Schema DSL: `Schema.refine` refinements-only; generalize `Schema.convert` to fallible for parse fields; remove `Refinement.parsing` routing.
- [ ] `[<DeriveRefinedSchema>]` emitting the promotion; `[<SchemaConstructor>]` as the smart-ctor hook.
- [ ] Contract as interface (static-abstract member) for owned types + SRTP for third-party; verify Fable.
- [ ] Packages: `Axial.Parse`/`Refined`/`Check`/`Result` under `Axial.ErrorHandling`; `Predicate` stays in Check; keep the `Axial.` prefix.
- [ ] Ship as a breaking major.
