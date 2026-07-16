# Contract Grammar Sketch

Status: grammar library + generator IMPLEMENTED (2026-07-12) in `src/Axial.Schema.Contracts` (parser, resolver,
emitter) with the `scripts/schemagen` CLI (`--check` drift guard) and `tests/Axial.Schema.Contracts.Tests`
(parser/resolver specs + byte-for-byte golden emission against the compiled corpus in
`tests/Axial.Schema.Tests/contracts/`). Scope shipped: single-version contracts, primitives + email format type,
comparisons/min/max/pattern/multipleOf/distinct, defaults, literal unions (→ generated DUs + `Value.enumOf`),
`list`/`map`, contract refs, inline tagged unions (`union kind { ... }` → `Value.unionInline`), doc comments
(→ XML docs + `Value.describe`/`Schema.describe`). Emitted shape per contract: public record (the draft), `schema`,
`validate` (→ `Result<Model<'t>, Diagnostics<SchemaError>>`), `parse`, and a typed `Fields` module of `FieldRef`s.
Multi-version generation shipped 2026-07-16: one file declares a contiguous oldest-first version chain, superseded
versions emit frozen suffixed types (`ConfigV1`), and the latest module gains a `contract` builder taking each
typed n-1 → n migration as a parameter plus the `VersionSource` (see `dev-docs/decisions/README.md`). User guide:
`docs/schema/contracts.md`. Deliberately not shipped yet: `check` refs, refined type refs, date/time/guid
defaults, LSP. Contracts are the WIRE tier only — domain models are hand-written F#
(see `docs/schema/trusted-construction.md`); a domain-tier `model` declaration kind was designed and rejected
(no methods on generated types, DUs don't fit the grammar). The versioned-Contract engine this grammar will
eventually target is shipped as `Axial.Schema.Contract` (`src/Axial.Schema/Contract.fs`). Original sketch follows;
this file refines the declaration grammar and the tooling plan.

## Universal Domain Authoring: Considered and Rejected (2026-07-17)

A design thread explored making contracts the authoring surface for *all* domain models — generated types
"blended" with user code via augmentation or a type-ownership toggle, MSBuild-integrated generation, top-level
union declarations. Rejected after taking stock; recorded so it is not re-litigated casually:

- **The market has run this experiment.** protobuf, Avro, and TypeSpec all offer IDL-first modeling with mature
  generators; .NET teams universally converged on IDL-at-the-edge with hand-written domain types. An IDL
  authoritative over the domain routes every domain change through a foreign syntax and a generator.
- **F# adopters chose F# for its type language.** Records, DUs, exhaustive matching are the product they already
  bought; "author your domain in our DSL" attacks the adopter's own motivation. The STJ+DTO developer's mental
  model ("my types are the truth") is violated from the other side.
- **The coverage trap is real at domain scope.** Universal authoring makes every F# type feature (interfaces,
  custom equality, struct-ness, generic DUs) and every serialization format's load-bearing semantics (protobuf
  field numbers, MessagePack integer keys) a grammar feature request. Multi-format serialization belongs at the
  Schema *interpreter* layer (the `Json.compile` pattern), never in the grammar.

The settled positioning that replaces it — **two schema tiers**: wire DTOs are permissive schemas shaped per
format (accept what the format allows, light constraints); domain models are strict hand-written F# (invariants,
constructors, DUs). The wire result maps to the domain through an ordinary function, which is where strictness
lives. Versioning (the `Contract` engine) applies to the wire tier when stored payloads must keep parsing.
Contracts generate the wire tier concisely; they are never the domain.

Consequences for ideas raised in the same thread:

- **`@constructor` refs and `extern` field-schema refs: parked, likely unnecessary.** With strictness in the
  wire→domain map, the map *is* the constructor; permissive wire schemas rarely need refined field types beyond
  the existing format types. Revisit only if dogfooding shows wire-tier validation gaps.
- **Type-ownership toggle (contract targets a hand-written type): dropped** with universal authoring — wire DTOs
  are generated records by definition.
- **MSBuild targets package (Grpc.Tools-style `.targets` running schemagen before compile): still queued.**
  Friction removal is good under any positioning; checked-in emission stays the default so generated code remains
  reviewable, with the target keeping it fresh in place of `--check`.
- **Record → schema generation for wire DTOs** (the reverse direction: you write the record, generation derives
  the permissive schema, STJ-familiar) is under consideration as the low-ceremony wire-tier entry —
  see `schema-source-generation.md`, which this re-scopes.

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

Single grammar library, three frontends (generator, LSP, tests). Investigated 2026-07-16; the plan below is
settled, the build is gated on dogfooding (see Sequencing).

**Distribution: one dotnet tool with subcommands** — `axial-contracts generate | check | lsp` — not a separate
LSP executable.

- The audience is F# developers with the .NET SDK by definition, so the zero-install pressure that pushes other
  language servers toward self-contained binaries does not exist. Repo-local tool manifests pin the tool version
  next to the `.contract` files it validates; teams already use this pattern for fantomas and fsautocomplete.
- Folding `scripts/schemagen` into the tool also gives external users a way to install the generator at all
  (today it is a repo-internal script), and the LSP can never disagree with the generator about the grammar
  because they are the same assembly.
- Mechanics: a `src/Axial.Schema.Contracts.Tool` project with `PackAsTool` + `ToolCommandName axial-contracts`,
  absorbing `scripts/schemagen/Program.fs` as `generate`/`check`. `Axial.Schema.Contracts` is dependency-free
  (it does not reference `Axial.Schema`; it emits text), so the tool's closure stays tiny. Both projects are
  `IsPackable=false` today and would flip on.
- Rejected: bundling per-RID NativeAOT binaries in the VS Code extension or on GitHub releases (build matrix and
  a second release process, for an audience that provably has the SDK); MSBuild analyzer/source-generator shape
  (`.contract` is not F# source, and checked-in generation is the point).

**Protocol layer: `Ionide.LanguageServerProtocol`** (F#-native, powers FsAutoComplete and csharp-ls, actively
maintained, netstandard2.0). The old `OmniSharp.Extensions.LanguageServer` shows maintenance drift (unmerged
bumps, stalled .NET Foundation migration); hand-rolled JSON-RPC would re-derive protocol types for no benefit.

**Server model.** Scan the workspace for `*.contract`, overlay dirty editor buffers, re-run
`Parser.parse` + `Resolver.resolve` on every change (files are tiny; no incrementality), and map
`ContractDiagnostic` to LSP diagnostics. Feature ladder:

- **Diagnostics first** — parse and resolution errors are already line-precise and are ~80% of the value.
  `ContractDiagnostic` carries a line but no column, so v1 publishes whole-line ranges; adding column spans to
  the tokenizer later is mechanical.
- **Hover / completion / go-to-definition** need the resolver to expose its resolution model (the
  contract/version registry, per-field resolved types) instead of returning only diagnostics — a modest refactor
  to return a resolution record alongside the diagnostic list. Version-*pin* warnings (reference pins a
  superseded version) come from the same data; true version-gap warnings (v3 exists, no v2→v3 migration) are not
  knowable from `.contract` files alone because migrations live in F# call sites.
- **Deferred**: formatting (no formatter exists), semantic tokens, `check`-name manifest (`check` refs are still
  rejected by the resolver, so there is nothing to complete).

**Editors.** Neovim/Helix/Emacs need only user config pointing a generic LSP client at `axial-contracts lsp` —
works day one with zero code. VS Code needs a thin extension regardless of distribution (something must declare
the language id): ~100 lines of TypeScript over `vscode-languageclient` spawning the tool (config path → local
manifest → global fallback, the fsautocomplete pattern) plus a TextMate grammar for offline highlighting. The
extension is the only piece outside the F# codebase and ships separately, after the tool.

**Sequencing.** Unchanged from the PRD: dogfood on the real config system first — that decides which diagnostics
matter in practice (likely: stale-`.g.fs` drift surfaced in-editor, superseded-pin warnings) before investing in
completion/hover. First build step when ready: the tool restructure + a diagnostics-only `lsp` subcommand
(roughly a day given the reuse), VS Code extension as a follow-up.

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
