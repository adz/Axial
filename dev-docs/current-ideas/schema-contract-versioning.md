# Schema Contract Versioning Sketch

Status: shipped (2026-07-13). `Axial.Schema.Contract` is the version-selection and typed-migration engine described
here. The remaining generator work is deliberately separate: `schemagen` still emits one version at a time and does
not generate migration-chain wiring.

The shipped engine represents a named, versioned family of frozen schemas with manually written, typed migrations.
Motivating boundaries are config files, messages, event-sourced events, and, later, database records.

## Fundamental Principle

**The domain model is never versioned.** External contracts are versioned; the current contract constructs the
domain (where refined types enter), and rules/flows see only the domain. Business logic stays independent of
representation evolution.

## The Idea

A schema stops being "the description of my current type" and becomes a version chain:

```fsharp
module SignupContract =
    module V1 =
        type Signup = { Email: string }
        let schema = ...   // explicit pipeline, frozen forever

    module V2 =
        type Signup = { Email: string; Age: int }
        let schema = ...   // current

    let contract =
        Contract.create "signup" V2.schema
        |> Contract.supersedes V1.schema (fun (v1: V1.Signup) ->
            Ok { Email = v1.Email; Age = 18 })
```

- Raw input is parsed against the schema of the version it was written under, so it gets that version's validation
  exactly as authored. Migration then runs between **parsed** types — total, typed, unit-testable ordinary F# — never
  between raw representations.
- Migrations are n-1 → n only; older versions compose through the chain. Writes always emit the newest version.
- Superseded versions are frozen checked-in code: the version record and its schema never change again. This is the
  event-sourcing upcaster pattern generalized to config and messages.

## Shipped Design Decisions

1. **Version detection.** `VersionSource.Field` reads an explicit scalar field,
   `VersionSource.External` requires `Contract.parseWithVersion`, and `VersionSource.UnversionedMeans` assigns one
   registered version to unversioned input. There is no structural sniffing.
2. **Constraint drift.** V1-valid data can violate V2 constraints. After migrating, re-validate the result against the
   current schema so "valid instance" always means valid under the *current* contract. Migration signature is
   therefore fallible — `'prev -> Result<'next, MigrationError>` — with an infallible convenience overload.
   Concretely, this re-validation should be `Model.reconstruct` (see `dev-docs/decisions/README.md`), not a bespoke
   mechanism — a migration's job is producing a `'next` value, `Model.reconstruct` is what closes the loop back to
   "this value actually satisfies the current schema." The worked example above (`Ok { Email = v1.Email; Age = 18
   }`) builds the next-version record directly, unchecked — that's exactly the gap this decision says to close;
   the example should route through `Model.reconstruct next` (or, once generated per-version `construct` functions
   exist — see `schema-source-generation.md` — through the generated checked constructor directly) rather than a
   raw record literal.
3. **Lifecycle is not core.** Events migrate on read forever (stored events are immutable, so old versions stay
   permanently load-bearing); databases eventually migrate at rest and retire versions. Keep that policy split out of
   the core Contract type and in integration layers.
4. **Boundary/domain split.** Version records are plain public records of primitives — wire shapes constructed only by
   the parser and migrations. The *current* model is the refined one (`Value.refined` bridge; smart-constructor types
   such as `Email`), so direct construction in application code cannot be invalid and version records are visibly
   ingestion artifacts. Invariant-bearing constraints live in the refined types; schema-level constraints cover
   boundary concerns (presence, trimming, external names, representation).
5. **Trust and diagnostics survive the contract boundary.** Successful parsing returns `Model<'model>`, because the
   selected schema, every migration, and head revalidation have established the same trust claim as `Model.validate`.
   `ParseFailed` and `RevalidationFailed` retain `Diagnostics<SchemaError>` rather than choosing one error and losing
   its path. Contract selection errors remain separate from application failures.
6. **Chains are contiguous.** `Contract.supersedes` accepts only the immediately preceding positive integer version.
   This keeps the typed builder and the documented n-1 → n migration rule identical; sparse version labels would
   imply migrations that skip an unrepresented contract revision.

## Why This Reinforces Existing Decisions

- **No attributes / no generation for versions.** Attributes describe the current type only; a superseded version
  needs standalone frozen schema code, which is exactly what the explicit pipeline (with `Axial.Schema.DSL` for
  terseness) produces. When a version is cut, the previous head materializes as ordinary committed code anyway.
- **No Draft type.** "Draft vs valid" is already the pre-`Ok` state of `Model.parse` (`parsed.Result` +
  `parsed.Errors`). A contract adds version dispatch in front of that gate and migration behind it; it does not need a
  parallel draft concept for *interactive editing*. That said, a related but distinct "draft" need surfaced while
  designing `Model.construct` (see `schema-source-generation.md`): a publicly-constructible, untrusted record that
  bridges into a schema-checked trusted type without going through `Model.parse`'s untyped `RawInput` boundary.
  Version records (`V1.Signup`, `V2.Signup` above) already *are* that shape — plain public records, freely
  constructible, explicitly not the trusted current model. The distinction this section draws still holds: don't add
  a *separate* draft concept on top of versioning, because versioning already produces one.

## Remote Desired-State Configuration (motivating scenario)

A central editor sets desired config; devices ingest, attempt to apply, and report back.

- Shared rules mean the *same compiled schema code* in the browser editor (Fable) and on the device (.NET/AOT) — not
  reimplemented rules. The schema also drives the editor form via inspection metadata.
- Version skew: devices ingest older versions via the chain, but must cleanly reject *newer* versions than they know,
  reporting their highest supported version. Mixed fleets imply deliberate downgrade emission at the center — a
  separate lossy feature, not an accident of the chain.
- Report-back distinguishes **contract rejection** (bug or version skew — alarm) from **application failure** (valid
  config, device-local state prevented applying it — expected operational case). Schema constraints can never capture
  device-local state.

## What The Author Writes (position as of 2026-07-07, scale-dependent)

The "contract declaration" question — should a richer declaration generate the representation record? — is
scale-dependent, not settled against generation:

- **Small contracts:** the version module (plain record + DSL schema) is the declaration. It already expresses wire
  names, constraints, optionality, nesting, and docs, and already derives codec, JSON Schema, docs, and inspection.
  Generation buys little here.
- **Large contracts (many fields, submodels, several live versions):** the record and constructor are mechanical
  restatements of the schema pipeline — roughly two-thirds of the text — and the hand-written N-ary constructor has a
  real hazard: positional alignment is type-checked but not name-checked, so swapping two adjacent same-typed fields
  compiles and silently transposes data. Generating record + constructor + schema from one field list eliminates both
  the volume and the swap hazard. This justifies declaration-first generation at scale.
- **The declaration input is a minimal external grammar, not an F# data value.** An F# data DSL was considered and
  dropped: evaluated F# cannot give identifier-as-field-name with preserved declaration order (anonymous records
  alphabetize; strings bring quotes and per-line keyword ceremony), so it pays generation's costs without the
  authoring win. The grammar is deliberately tiny and line-oriented:

  ```text
  contract Config.3 {
    deviceId:    text [ required ]
    pollSeconds: int  [ atLeast 1 ]
    tanks:       list Tank.2
  }
  ```

  No expressions ever: constraint arguments are literals; refined types, custom value schemas, migrations, and domain
  construction stay in F# and are referenced by name. That keeps the parser ~small with line-precise diagnostics and
  "it's a text file" editor support. Out-of-file references resolve at generation time rather than typecheck time —
  errors still fail the build (generated F# will not compile), one step removed. The generator emits checked-in
  ordinary F# (version record, `create`, typed schema), so AOT/Fable/tooling are untouched and downstream machinery
  still consumes plain record + schema. The earlier TP objections do not apply to this route; the moment the grammar
  grows expressions, they do.
- **Migrations are plain F# functions**, not structured data. Structured migrations need escape hatches, which
  collapse back into arbitrary code with worse ergonomics. Middle path kept open: optional advisory metadata beside
  the function (`Renamed`, `Derived from`, external dependencies, purity) to power lineage reports and upgrade docs —
  add that vocabulary only when a consumer wants the reports.

Per-boundary lifecycle notes: config is often rewritten to latest on save; messages/integration events emit latest
and upcast old on read; event stores upcast on replay forever; database rows use ordinary DB migrations, with
contract migrations only for serialized blobs.

## Rejected Alternatives

- **External schema language + type provider.** Generative TPs cannot emit F# records (the models would stop being
  records), TPs do not run under Fable, and TP tooling cost is the highest in the ecosystem. An external language also
  inverts ownership of the domain types and adds a parser/diagnostics/editor-support burden.
- **Attribute-driven generation as the versioning surface.** See above; generation (per
  `schema-source-generation.md`) remains a possible sugar for the *head* version only.
- **A rich external contract language.** Anything with expressions, computation, or embedded logic rebuilds a
  language and its tooling burden. Only the minimal shape-only grammar above is in scope.
- **An F# data DSL as the declaration input.** Names become string literals and field lines carry keyword ceremony;
  order-preserving identifier names are not expressible in evaluated F# data. Worst of both worlds at scale.
- **Migrations as structured data ("contract AST").** Only pays off if migration stops being arbitrary code, but
  custom migrations always need escape hatches; advisory metadata beside plain functions captures the inspectability
  benefits incrementally.

## Follow-up Boundary

Multi-version generator support remains follow-up work. Lift the resolver's duplicate-contract-name rejection, emit
per-version modules, and generate chain wiring only after a real multi-version config has exercised the hand-written
engine. LSP positioning remains gated on that dogfood pass.
