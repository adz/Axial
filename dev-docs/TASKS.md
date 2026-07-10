# Axial Tasks

This is the active development queue. Keep completed work out of this file because loop scripts consume it directly.
Keep live architecture direction in `dev-docs/PLAN.md`.
Keep speculative design sketches in `dev-docs/current-ideas/` and high-level durable decisions in
`dev-docs/decisions/`.

Work this queue from top to bottom.

Axial has two main groups, and everything in this queue serves that split:

- **Parse-don't-validate results**: `Schema` is the front door for domain models — parsing, validation, redisplay,
  rules, and metadata fall out of one declaration. Plain `Result` with the user's own error DU is the blessed lane for
  simple code. `Check`, `Validation`, `Refined`, and the interpreter error types are machinery behind those two doors,
  not peer entry points.
- **Effects in Flow**: the ZIO-style Reader-Async-Result workflow model. Useful with or without schemas, and never part
  of the entry price for the results group.

Phases 19–24 are complete: the two-door narrative, one boundary error, one domain-value catalog, union schemas,
boundary utility packages (`Axial.Codec`, `JsonSchema.generate`, `RawInput.ofJsonDocument`, the `examples/Axial.Api`
minimal-API sample), and positioning/polish (comparison pages, the public AOT/Fable story, the backtick audit with
`Policy.lift`). The `dotnet new axial-api` template is evaluated and deferred in `dev-docs/decisions/README.md`.

Phase 25 (reiteration-question triage) is complete: all twelve questions from `dev-docs/questions.md` were decided.
Deferred items (codec decode allocations, checked-codec mode, UI-metadata promotion, fused boundary path, netstandard2.1
STJ adapter) are recorded with their pre-chosen answers in `dev-docs/decisions/README.md`; the rest became Phase 26.

Phase 26 (triaged boundary work) is complete: union wire shapes (`Value.enumOf`, `Value.unionInline`) across
Input.parse, Codec, JsonSchema, and Inspect, and the C# ergonomics audit (`RawInput.ofDictionary`,
`RawInput.ofConfigurationPairs`, `Input.parseWithOptions`, and "From C#" sections on the codec and input-sources
pages).

Phase 27 (contract grammar prerequisites) is complete: `Value.map` (JSON objects as dictionaries, string-keyed) across
Input.parse, Codec, JsonSchema (`additionalProperties`), and Inspect; default-value metadata (`Value.withDefault` /
`Value.defaultValue`) with JSON Schema `default` lowering; and a `multipleOf` schema constraint lowered to both
JSON Schema and an executable check.

Phase 28 (Contract versioning/migration core) is next. Detail below.

## Phase 28: Contract versioning/migration core

### Motivation

Axial's 1.0 gate is driven by a real adoption target: a ~100-variant versioned config system (`dev-docs/PLAN.md`).
The concrete scenario is a **remote desired-state device config** setup: a central editor writes desired config: a
fleet of devices (mixed versions, .NET/AOT and Fable) ingest it and report back. Requirements that fall out of that
scenario:

- The domain model is never versioned — only the external wire representation is. Business logic (`RuleSet`, Flow
  workflows) must stay independent of representation churn as the config shape evolves across dozens of versions.
- Devices must accept older config versions via typed migrations (n-1 → n, composing through the chain), but cleanly
  reject versions newer than they know, reporting their highest supported version — this is a fleet-skew concern, not
  an edge case.
- Callers must be able to tell **contract rejection** (bug or version skew — alarm-worthy) apart from **application
  failure** (config parsed and validated fine, device-local state prevented applying it — expected operational case).
  That split has to show up in the returned error type.
- Version detection must be flexible across boundaries: an envelope field inside the payload (the common case for
  config files and most messages), or an out-of-band value the caller already has (event-store/message metadata,
  transport headers) — never structural sniffing.

This phase builds the **runtime orchestration engine** only: version dispatch, chain composition, and post-migration
re-validation. It deliberately does not build the `.contract` declaration grammar or generator
(`dev-docs/current-ideas/contract-grammar.md`) — per that sketch's own sequencing, the grammar/generator is a later,
scale-gated concern, and per `schema-contract-versioning.md`'s Promotion Criteria this phase should not start without
a concrete consumer, which the work adoption target now provides. Each version in this phase is still hand-written as
an ordinary F# record + `Schema<'model>` (see `docs/error-handling/getting-started.md` / `src/Axial.Schema/Schema.fs`
`module Schema` DSL), and each migration is a hand-written function — Contract only supplies the engine that wires
those pieces together safely.

### Grounding in the current codebase

All new code lives in the existing `src/Axial.Schema/Axial.Schema.fsproj` project (namespace
`Axial.Validation.Schema`, matching `Input.fs`/`RawInput.fs`/`SchemaValidation.fs`) — no new project. It depends on:

- `Schema<'model>` — `src/Axial.Schema/Schema.fs:1031`, built via `module Schema` (Schema.fs:2075).
- `Input.parse (schema: Schema<'model>) (input: RawInput) : ParsedInput<'model, SchemaError>` —
  `src/Axial.Schema/Input.fs:472`.
- `RawInput` — `src/Axial.Schema/RawInput.fs`, a closed DU: `Missing | Scalar of string | Many of RawInput list |
  Object of Map<string, RawInput>`.
- Field-peek without full parsing (the version-detection hook):
  `RawInput.tryFindPath (path: string) (input: RawInput) : RawInput option` (RawInput.fs:594) and
  `RawInput.tryRedisplayPath (path: string) (input: RawInput) : string option` (RawInput.fs:625). Use
  `tryRedisplayPath` to pull the raw string at `"version"` (or whatever wire name is configured) before selecting
  which versioned `Schema` to parse with — this already exists and needs no new `RawInput` API.
- `SchemaValidation.validate (schema: Schema<'model>) (model: 'model) : Axial.Validation.Validation<'model,
  SchemaError>` — `src/Axial.Schema/SchemaValidation.fs:712`. Use this for post-migration re-validation (constraint
  drift: a migrated instance can violate the *current* schema's constraints even though it was valid under its own
  version). Convert with `Validation.toResult` (`src/Axial.ErrorHandling/Validation.fs`).
- `Result` helpers — `src/Axial.ErrorHandling/Result.fs`, `module Result`. Migration functions return plain
  `Result<'next, MigrationError>`.

### API design (decided in this planning conversation)

New file `src/Axial.Schema/Contract.fs`. Add it to `Axial.Schema.fsproj`'s `<Compile>` list after `Input.fs` and
before `RefinedSchemas.fs` (needs `Input.parse`, `RawInput`, `SchemaValidation.validate`, none of which depend on
it).

```fsharp
namespace Axial.Validation.Schema

/// Raised when a hand-written migration function fails, or when post-migration re-validation
/// against the current schema fails (constraint drift).
type MigrationError =
    | MigrationFailed of message: string
    | RevalidationFailed of SchemaError

/// Version-detection strategy for a Contract. Deliberately explicit — never structural sniffing.
type VersionSource =
    /// Peek this wire field in the raw payload before selecting a schema (the envelope case:
    /// config files, most messages). Resolved via RawInput.tryRedisplayPath.
    | Field of wireName: string
    /// Caller supplies the version alongside the raw payload (event-store/message metadata,
    /// transport headers). Use Contract.parseWithVersion, not Contract.parse, with this source.
    | External
    /// No version field/metadata present means this version. At most one per Contract.
    | UnversionedMeans of version: int

/// What went wrong resolving/parsing/migrating a contract instance. Consumers should treat
/// VersionTooNew/VersionUnrecognized/VersionMissing/Migration as contract rejection (alarm-worthy:
/// bug or version skew), and keep this distinct from any later "valid config, couldn't apply it
/// locally" application-level error, which is out of scope for Contract itself.
type ContractError =
    | VersionMissing
    | VersionUnrecognized of version: int
    | VersionTooNew of detected: int * highestSupported: int
    | ParseFailed of version: int * SchemaError
    | Migration of MigrationError

/// Builder tracking the model type ('model, fixed = the head/current version) and the type of
/// the newest version registered so far in the chain ('current). supersedes shifts 'current back
/// one version each call, giving compile-time checked chain composition without an untyped AST.
type ContractBuilder<'model, 'current> = internal { ... }

/// A built, immutable contract: head version/schema plus the full prior-version chain, type-erased
/// internally (boxed) so `Contract<'model>` only exposes the current model type.
type Contract<'model> = internal { ... }

module Contract =
    /// Start building a contract at its current (head) version and schema.
    val create<'model> : name: string -> headVersion: int -> headSchema: Schema<'model> -> ContractBuilder<'model, 'model>

    /// Register the version immediately prior to the newest version registered so far, with its
    /// own frozen schema and a migration from it to that newer version. Call newest-to-oldest,
    /// mirroring the doc's worked example:
    ///   Contract.create "signup" 2 V2.schema
    ///   |> Contract.supersedes 1 V1.schema (fun (v1: V1.Signup) -> Ok { Email = v1.Email; Age = 18 })
    ///   |> Contract.build (Field "version")
    val supersedes : version: int -> schema: Schema<'prev> -> migrate: ('prev -> Result<'current, MigrationError>) -> ContractBuilder<'model, 'current> -> ContractBuilder<'model, 'prev>

    /// Finalize the builder into a Contract, fixing its version-detection strategy.
    val build : source: VersionSource -> ContractBuilder<'model, 'oldest> -> Contract<'model>

    /// Parse raw input whose version is embedded per the contract's VersionSource (Field or
    /// UnversionedMeans). Detects version, parses against that version's frozen schema, walks the
    /// migration chain up to the head version, then re-validates against the head schema.
    val parse : contract: Contract<'model> -> raw: RawInput -> Result<'model, ContractError>

    /// Parse raw input whose version is supplied externally (VersionSource.External, or to
    /// override Field/UnversionedMeans detection e.g. from a transport header).
    val parseWithVersion : contract: Contract<'model> -> version: int -> raw: RawInput -> Result<'model, ContractError>

    val name : Contract<'model> -> string
    val headVersion : Contract<'model> -> int
    val headSchema : Contract<'model> -> Schema<'model>
```

Internal storage sketch for `Contract<'model>` (implementation detail, adjust as needed): a
`Map<int, VersionEntry>` keyed by version number, where

```fsharp
type private VersionEntry =
    { ParseRaw: RawInput -> Result<obj, SchemaError>          // boxed Input.parse for that version's schema
      MigrateToNext: obj -> Result<obj, MigrationError> }     // boxed migrate function to the next version up
```

`Contract.supersedes` closes over the caller's `Schema<'prev>` and `'prev -> Result<'current, MigrationError>`,
boxing both into a `VersionEntry` at that version number. `Contract.parse`/`parseWithVersion`:

1. Resolve the version int per `VersionSource` (`Field` → `RawInput.tryRedisplayPath wireName raw` then
   `Int32.TryParse`; `UnversionedMeans v` as fallback when the field/metadata is absent; `External` requires
   `parseWithVersion`). Missing with no fallback → `ContractError.VersionMissing`.
2. If `version = headVersion`: `Input.parse headSchema raw`, map failure to `ParseFailed`, return the model directly
   (already current, no migration or re-validation needed).
3. If `version > headVersion`: `ContractError.VersionTooNew (version, headVersion)`.
4. If `version` matches a registered prior entry: `ParseRaw raw` (→ `ParseFailed` on failure), then fold
   `MigrateToNext` from that version up through the chain to the head version (→ `ContractError.Migration` on
   failure), then `SchemaValidation.validate headSchema model |> Validation.toResult` and map failure to
   `ContractError.Migration (RevalidationFailed _)`.
5. Otherwise (no head match, no chain entry, not covered by `UnversionedMeans`): `VersionUnrecognized version`.

### TODOs

1. Add `src/Axial.Schema/Contract.fs` implementing the types and `module Contract` above; register it in
   `Axial.Schema.fsproj`'s `<Compile>` list after `Input.fs`, before `RefinedSchemas.fs`.
2. Unit tests in `tests/Axial.Schema.Tests` (new file, e.g. `ContractTests.fs`, added to that project's `.fsproj`)
   covering: head-version parse; single-hop migration (V1 → V2) with `Field` version source; multi-hop chain (V1 →
   V2 → V3) exercising composed migration; `UnversionedMeans` fallback when the field is absent; `VersionTooNew` when
   detected version exceeds head; `VersionUnrecognized` for an unknown version number; constraint-drift
   re-validation failure surfaced as `Migration (RevalidationFailed _)` (e.g. a V1 record that migrates to a value
   violating a V2-only `atLeast`/`multipleOf` constraint); a failing hand-written migration surfaced as
   `Migration (MigrationFailed _)`; `parseWithVersion` with `VersionSource.External`.
3. Confirm Fable compatibility — `Axial.Schema.fsproj` targets `netstandard2.1;net8.0` and ships `Fable.Core`; avoid
   any BCL API in `Contract.fs` not already used elsewhere in this project (boxing/`Map`/`Int32.TryParse` are fine;
   check against existing patterns in `RawInput.fs` if unsure).
4. Once the implementation is verified end-to-end (round-trip a 3-version chain), update
   `dev-docs/current-ideas/schema-contract-versioning.md`: mark "Design Decisions To Settle" #1 (version detection)
   resolved with the `VersionSource` design above, and update the Promotion Criteria note to record that the
   ~100-variant work config system is the concrete consumer that opened this work.
5. Do not start `dev-docs/current-ideas/contract-grammar.md` (the `.contract` declaration grammar/generator/LSP)
   until this phase's engine is dogfooded against at least one real multi-version config from the adoption target —
   per that sketch's own sequencing (versioning/migration machinery → grammar and generator → dogfood → LSP).

## Acceptance Checks

The two-group direction is coherent when the following are true:

- the README and getting started teach exactly two doors: Schema for domain models, plain `Result` for simple code
- a newcomer handles one error shape at the boundary, with one default renderer to display strings
- domain value types exist in one catalog usable standalone and as schema fields
- discriminated unions are expressible as schemas with path-aware diagnostics
- a runnable ASP.NET Core sample serves parsing, error responses, and OpenAPI from one schema declaration
- one schema declaration also compiles to a trusted-lane JSON codec with benchmarked performance
- Flow is never required by the results-group quick starts
- comparison pages answer FsToolkit.ErrorHandling, FluentValidation, and zod by name
- generated reference docs match source comments
