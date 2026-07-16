# Record → Schema Generation for Wire DTOs

Status: DESIGNED 2026-07-17, all open questions resolved below; not yet built. Supersedes the earlier
domain-tier `[<Schema>]` source-generation sketch (preserved in git history), which was re-scoped by the
two-tier decision (`dev-docs/decisions/README.md`, 2026-07-17): generation from records targets **wire-format
DTOs only**. Domain models stay hand-written; strictness lives in the wire→domain mapping function.

## The Shape of the Feature

Two entry points produce the same wire tier, and the choice is only about who owns the record:

- **`.contract` files** — the declaration owns record + schema + version chain. Best at scale.
- **Record → schema (this design)** — *you* own a plain F# record, the generator derives the permissive schema.
  Best for the developer coming from `System.Text.Json` + DTOs: write the record you would have written anyway,
  mark it, and parsing/validation/JSON Schema/field references appear without authoring any schema pipeline.

Both lower into the **same internal AST (`ContractDecl`)** and flow through the same `Resolver` and `Emitter`.
Record input is a second parser frontend, not a second pipeline: one constraint vocabulary, one diagnostics set,
one emitted shape, no drift between the two entry points. The only emitter difference is a per-declaration
`OwnsType` flag: `.contract` declarations emit the record; record-derived declarations emit **only the schema
module**, targeting the user's type — so the F# compiler is the drift detector (rename a field and the checked-in
`.g.fs` record literal stops compiling, pointing at the exact field).

## Input: a marked record, optionally annotated

```fsharp
// wire/orders.fs — ordinary user-owned F#
namespace MyApp.Wire

open Axial.Schema.Wire

[<WireSchema>]
type OrderWire =
    { /// Stock keeping unit.
      [<Pattern @"^[A-Z]{3}-\d+$">]
      Sku: string
      [<AtLeast 1>]
      Quantity: int
      [<WireName "customer_note">]
      Note: string option }
```

A bare `[<WireSchema>]` record with no other annotations is fully valid and generates a shape-only permissive
schema — the STJ-familiar zero-ceremony case. Annotations are opt-in refinements.

Resolved decisions:

- **Attributes are the annotation mechanism.** The earlier anti-attribute stance was about *domain* schema
  authoring and versioning; neither objection applies here. Wire DTOs are exactly where .NET developers already
  expect attributes (`JsonPropertyName`, MessagePack `Key`), attributes cannot hide logic (constant arguments
  only — the zero-expression rule in F#-hosted form), and frozen superseded versions keep their own attributed
  records, so version-freezing works.
- **Attribute vocabulary mirrors the `.contract` constraint grammar one-to-one**, nothing more:
  `WireSchema` (marker; optional named args below), `WireName` (per-field wire-name override), `Pattern`, `Min` /
  `Max` (size of text/list/map), `AtLeast` / `GreaterThan` / `AtMost` / `LessThan` / `MultipleOf` (numeric value),
  `Distinct`, `Email`, `Default`. New constraint ideas go into the grammar AST first and both frontends pick them
  up — never attribute-only features.
- **Attributes live in `Axial.Schema` under the `Axial.Schema.Wire` namespace.** Inert attribute classes, zero
  dependencies, Fable-safe metadata; no new package. They are read at generation time from *source text*, never
  by runtime reflection, which keeps Requirement 4 intact and sidesteps attribute constant limits (a
  `[<AtLeast 0.5>]` on a decimal field is read as the literal source text and parsed as decimal exactly).
- **Doc comments**: `///` on the record and fields flow to `Schema.describe` exactly as contract doc comments do.
- **Wire names**: default is the camelCased field name (`MarketingOptIn` → `marketingOptIn`), matching STJ's
  default and matching what `.contract` authors write by hand; a `--wire-naming` CLI option sets the policy per
  generation run, `[<WireName>]` overrides per field. Not resolved by reading STJ's `JsonPropertyName` — owning
  the vocabulary avoids coupling to another library's semantics (noted as a possible future adoption bridge,
  deliberately not v1).

## Admitted Field Types

Exactly the grammar's vocabulary, mapped back from F#; anything else is a generation-time diagnostic with
guidance:

| F# field type | Lowers to |
|---|---|
| `string` | `text` (`Email` attribute ⇒ email format) |
| `int`, `decimal`, `bool` | same primitives |
| `System.DateOnly`, `System.DateTimeOffset`, `System.Guid` | `date`, `dateTime`, `guid` |
| `'t option` | optional field (one absence axis, same rules: no defaults on optionals) |
| `'t list` | `list T` |
| `Map<string, 't>` | `map T` |
| another `[<WireSchema>]` record | contract reference (self-reference ⇒ `Schema.defer`) |
| nullary-case DU | literal union / enum (wire tags = camelCased case names, `WireName` on cases overrides) |
| DU marked `[<WireUnion "kind">]`, all cases carrying one marked-record payload | internally tagged union |

Rejected with diagnostics: `float`/`float32` (wire numbers are `decimal` in this vocabulary), `int64`, arrays,
tuples, anonymous records, generic records, private representations (wire DTOs are public by definition — this
design dissolves the old sketch's private-constructor wall rather than solving it). Records may freely carry
members and interface implementations; the generator reads fields only. Struct records are fine.

## Version Chains from Records

The emitter's own multi-version output convention is the input convention, so the two directions are symmetric:
`ProfileV1`, `ProfileV2`, … are frozen superseded versions and the bare `Profile` is current. All must carry
`[<WireSchema>]`, live in one file, and be contiguous — the existing resolver rules apply unchanged because the
frontend lowers each record to a versioned `ContractDecl` (`XxxVn` ⇒ version n of chain `Xxx`; a bare marked
record with no `Vn` siblings is simply version 1 of itself). `[<WireSchema(Chain = "Profile", Version = 2)>]`
overrides the convention for names it doesn't fit. The generated module for the current version gains the same
`contract` builder taking each typed n-1 → n migration as a parameter — identical emission to the `.contract`
path.

## Tooling

- **The frontend parses F# source with FSharp.Compiler.Service, syntax tree only** (`ParseFile`, no type
  checking). Hand-rolling an F# subset parser is a tar pit; FCS syntax parsing is the ecosystem-standard move
  (Fantomas, Myriad) and everything needed — attributes, fields, types, XML docs, namespace — is in the untyped
  tree. The restricted type vocabulary is validated by the frontend itself, so no checked/typed tree is needed.
- **FCS is a dependency of the tool only**, never of any package. `schemagen` (later `axial-contracts generate`)
  accepts `.fs` inputs alongside `.contract` inputs; `.fs` files are scanned for `[<WireSchema>]` and lowered.
- **One resolution set**: record-derived and `.contract`-derived declarations register in the same registry, so a
  `.contract` can reference a record-defined wire type (`Profile.v2`) and vice versa. Same-file declaration-order
  rules apply per source; the fsproj compilation order of `.g.fs` siblings is the user's usual responsibility.
- **Output**: sibling `x.g.fs` per input file, checked in, byte-for-byte `--check` drift guard in CI — identical
  to contracts. The `.g.fs` takes its namespace from the source file's namespace declaration (`--namespace`
  applies only to `.contract` inputs). The planned MSBuild targets package covers both input kinds with one
  before-compile hook.
- **Emitter changes**: `ContractDecl` gains `OwnsType: bool` (and enum/union nodes gain an optional external type
  name, since a user DU's name and cases are fixed rather than generated). When `OwnsType = false` the emitter
  skips the record and case-DU declarations and emits the module against the user's names; everything else —
  `schema`, `validate`, `parse`, `Fields`, the multi-version `contract` builder — is shared emission.

## Generated Output (for the example above)

```fsharp
// orders.g.fs — generated, checked in
namespace MyApp.Wire

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module OrderWire =

    let schema : Schema<OrderWire> =
        Schema.recordFor<OrderWire, _> (fun sku quantity note ->
            { Sku = sku
              Quantity = quantity
              Note = note })
        |> Schema.field "sku" _.Sku (Schema.text |> Schema.constrainAll [ Constraint.pattern ("^[A-Z]{3}-\\d+$") ] |> Schema.describe "Stock keeping unit.")
        |> Schema.field "quantity" _.Quantity (Schema.int |> Schema.constrainAll [ Constraint.atLeast (1) ])
        |> Schema.field "customer_note" _.Note (Schema.option Schema.text)
        |> Schema.build

    let validate (draft: OrderWire) : Result<OrderWire, Diagnostics<SchemaError>> = ...
    let parse (input: RawInput) : ParsedInput<OrderWire, SchemaError> = ...
    module Fields = ...
```

The record literal in the constructor lambda and the `_.Sku` getters are the compile-time contract with the
user's record: any rename, addition, removal, or type change makes the stale `.g.fs` fail to compile with an
error at the exact field, until regeneration.

## Deliberately Not Doing

- No runtime derivation, no reflection, no type providers — build-time source-to-source only, same as contracts.
- No domain-tier targets: the old sketch's `[<Schema>]`-on-domain-records idea, the generated `construct`
  function, and private-representation support are all superseded by the two-tier positioning (the wire→domain
  mapping function is the constructor; `FieldRef` shipped the optics idea).
- No STJ attribute interop in v1, no `allOf`/untagged unions (same grammar limits), no attribute-only constraint
  vocabulary.

## Implementation Order

1. `Axial.Schema.Wire` attribute classes (inert, in `Axial.Schema`).
2. AST extension (`OwnsType`, external enum/union type names) + emitter mode; golden tests over hand-written
   `ContractDecl` values prove the module-only emission before any frontend exists.
3. FCS frontend: `.fs` → `ContractDecl list` with frontend diagnostics (unsupported types, chain conventions);
   spec-tested against a record corpus like the parser is against the `.contract` corpus.
4. schemagen accepts `.fs` inputs; golden corpus pair + behavior tests in `Axial.Schema.Tests` (including one
   record-defined version chain wired through `Contract.parse`).
5. Docs: a "start from a record" section in `docs/schema/contracts.md` (or a sibling page) teaching this as the
   low-ceremony wire-tier entry; comparison note against STJ+DTO.
