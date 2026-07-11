# Contract Grammar Sketch

Status: grammar library + generator IMPLEMENTED (2026-07-12) in `src/Axial.Schema.Contracts` (parser, resolver,
emitter) with the `scripts/schemagen` CLI (`--check` drift guard) and `tests/Axial.Schema.Contracts.Tests`
(parser/resolver specs + byte-for-byte golden emission against the compiled corpus in
`tests/Axial.Schema.Tests/contracts/`). Scope shipped: single-version contracts, primitives + email format type,
comparisons/min/max/pattern/multipleOf/distinct, defaults, literal unions (→ generated DUs + `Value.enumOf`),
`list`/`map`, contract refs, inline tagged unions (`union kind { ... }` → `Value.unionInline`), doc comments
(→ XML docs + `Value.describe`/`Schema.describe`). Emitted shape per contract: public record (the draft), `schema`,
`validate` (→ `Result<Model<'t>, Diagnostics<SchemaError>>`), `parse`, and a typed `Fields` module of `FieldRef`s.
Deliberately not shipped yet: multiple versions/migrations (Contract machinery stays gated), `check` refs, refined
type refs, date/time/guid defaults, LSP. Contracts are the WIRE tier only — domain models are hand-written F#
(see `docs/schema/trusted-construction.md`); a domain-tier `model` declaration kind was designed and rejected
(no methods on generated types, DUs don't fit the grammar). Original sketch follows. Companion to `schema-contract-versioning.md` — that file holds
the Contract concept; this file refines the declaration grammar and the tooling plan.

## Goals

- Cover the full practical JSON Schema feature surface as pure shape data.
- Zero expressions, ever: literals and names only. The grammar can *name* semantics (checks, refined types, nested
  contracts) but never *define* them; every name must resolve to an F# symbol in generated output.
- One F# library owns the grammar (lexer, parser, AST, resolver, formatter), consumed by the generator, the LSP
  server, and tests. No second implementation, no drift.

## Design Rules

The grammar fits in five rules:

1. **Field line:** `name(?) (as "wire")?: type ([constraints])? (= default)?` — concerns strictly left to right:
   naming, type, constraints, value. Wire name defaults to the field name.
2. **Types are a head word plus arguments** (`list Tank.v2`, `map decimal`, `union kind { ... }`), and every brace
   block contains more `name: type` lines — the contract body and union body share one shape. A set of literals is a
   type: `"auto" | "manual" | "off"`.
3. **Constraints are `word literal...`.** Comparisons (`>= > <= <`) bound a numeric *value*; `min`/`max` bound the
   natural *size* of the type (text length, list/map count); `check name` references a named F# check. One literal
   syntax everywhere: quoted strings, bare numbers, `true`/`false`.
4. **Required unless `?`.** There is no `required` word and no separate null notion — one absence axis, one marker.
5. **`///` doc comments above the line; `@annotation (literal)?` between doc and field.** Names are plain
   identifiers; the version is always `.vN` (`v`+digits is reserved), so references are unambiguous.

## Grammar By Example

```text
/// Site polling configuration.
contract Config.v3 {
  /// Stable device identifier issued by the registry.
  deviceId as "device_id": text [ pattern "^[A-Z]{3}-\d+$" ]
  pollSeconds: int [ >= 1, multipleOf 5 ] = 30
  mode: "auto" | "manual" | "off"
  tanks: list Tank.v2 [ min 1, distinct ]
  location?: Geo.v1
  thresholds: map decimal
  shape: union kind {
    circle: Circle.v1
    rect: Rect.v2
  }
  password: StrongPassword [ check entropyFloor ]
  @deprecated "use tanks[].siteId"
  siteId?: text
}
```

Notable precision choices: `map decimal` has string keys always (JSON object keys are strings; an arrow form would
imply a choice that does not exist). Literal unions may be lifted by the generator to real F# DUs. Exclusive numeric
bounds come free (`> 0`) instead of an `exclusiveMinimum` spelling.

## JSON Schema Coverage

| JSON Schema | Grammar | Lowering |
|---|---|---|
| `type` primitives | `text`, `int`, `decimal`, `bool` | existing `Value` primitives |
| `format` | `email`, `guid`, `date`, `dateTime`, `uri` as first-class type names | `Value.text` + `SchemaFormat` |
| `pattern`, `multipleOf` | constraint list | existing `SchemaConstraint`s; `multipleOf` is new |
| `minimum`/`maximum`/`exclusive*` | comparisons `>= > <= <` | lower to `atLeast`/`greaterThan`/... |
| `minLength`/`minItems`/`maxLength`/`maxItems` | `min n` / `max n` (size of the type) | lower to `minLength`/`minCount`/... |
| `enum` | literal union type `"a" \| "b"` | `oneOf` constraint; generator may lift to a DU |
| optionality / `null` / `required` | `?` suffix; required unless marked | one notion of absence, not three |
| `default` | `= literal` | new schema metadata (also wanted by the config editor) |
| arrays + item count/uniqueness | `list T [ min n, max n, distinct ]` | existing |
| `$ref` / nested objects | `Tank.v2` contract reference | `Value.nested` |
| dictionaries (`additionalProperties: schema`) | `map T` (keys always text) | **new `Value.map` needed in Axial** |
| `additionalProperties: false` | contract-level `@strict` (default) / `@open` | policy annotation |
| `oneOf` + discriminator | `union kind { tag: Contract.vN }` | `Value.union`, exactly its shape |
| `allOf`, untagged `anyOf` | **deliberately absent** | schema algebra / untagged unions are where JSON Schema stops being data; contracts want discriminators |
| `title`/`description` | `///` doc comments | XML docs, JSON Schema, LSP hover |
| `deprecated`, `examples`, `readOnly`/`writeOnly` | `@deprecated "msg"`, `@example ...`, `@readOnly` | metadata-only annotations |

Custom semantics tiers (from the versioning sketch): refined type as the field type (canonical), `check name` for
named custom constraints (metadata + F#-supplied `Check`), cross-field logic stays in `RuleSet` on the domain.

## Language Server

Single grammar library, three frontends (generator, LSP, tests).

- **Diagnostics**: parse errors; resolution errors (unknown type/contract/version/check, constraint–type mismatch,
  default-literal type mismatch, duplicate wire names); version-gap warnings (contract N exists, no N-1→N migration).
- **Completion**: types; constraints valid for the field's type; contract refs with known versions; check names via a
  manifest exported by the F# side (the generator needs this resolution map anyway).
- **Hover**: doc comment + resolved JSON Schema fragment for the field.
- **Go-to-definition**: contract refs → `.contract` files; checks/refined types → F# source via the resolution map.
- **Formatting**: canonical aligned-column style.
- **Semantic tokens**; TextMate fallback.

Ship as a `dotnet tool` LSP server + thin VS Code extension; other editors via generic LSP config.

## Sequencing

1. Axial prerequisites, useful independently: `Value.map`, default-value metadata, `multipleOf`.
2. Grammar library: AST, parser, resolver, formatter; spec-tested against a `.contract` corpus.
3. Generator: emit version record + `create` + DSL schema pipeline as checked-in F#.
4. LSP server over the same library; VS Code extension last.

## Open Points

- Nested contract version pinning: `list Tank.v2` pins a version; cutting `Tank.v3` forces a visible decision in every
  containing contract (bump + migrate, or stay pinned). Generator/LSP need a story (diagnostic? codemod?).
- Custom-constraint documentation: `check entropyFloor` names semantics a non-F# consumer cannot see; consider an
  optional doc string on `check`.
- Annotation set: keep the initial set minimal (`@deprecated`, `@strict`/`@open`, `@example`, `@readOnly`) and grow
  only with a consuming interpreter.
