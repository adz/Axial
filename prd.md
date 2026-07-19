# Axial Product Requirements

## Purpose

This document defines what Axial is, what its 1.0 must contain, and in what order the work ships. Live architecture
direction is `dev-docs/PLAN.md`; the active queue is `dev-docs/TASKS.md`; durable decisions are
`dev-docs/decisions/README.md`. This file changes only when the product definition changes.

Axial is an F#/.NET library with two groups, presented in this order:

- **Parse-don't-validate results.** `Schema<'model>` is the front door for domain models: one declaration drives input
  parsing, intrinsic validation, redisplay, contextual rules, JSON codecs, JSON Schema output, and metadata
  interpreters. Plain `Result` with a user-owned error DU is the blessed lane for simple code without domain models.
- **Effects in Flow.** `Flow<'env, 'error, 'value>`, a ZIO-inspired Reader-Async-Result workflow model with typed
  errors, explicit services and layers, scoped resources, and cancellation semantics. Useful with or without schemas,
  and never part of the entry price for the results group.

## Release Strategy

The boundary stack ships 1.0 first; the Flow group follows demand.

- **The 1.0 gate is the boundary stack**: the `Axial.ErrorHandling` package (hosting the `Axial.ErrorHandling`,
  `Axial.Validation`, and `Axial.Refined` namespaces), `Axial.Schema` (declaration + interpreters in one package,
  including `Schema.check` boundary admission and `ContextRules`), and `Axial.Codec`. Scope: the current queue in
  `dev-docs/TASKS.md` (schema-depth candidates, now that the contract versioning engine has shipped) plus its
  acceptance checks.
- **The Flow group's remaining pre-1.0 scope is demand-driven**, tracked in `LATER_TODO.md`. Its current surface
  (typed errors, services/layers, scoped resources, fibers, retry/timeout) is already useful and stays
  source-compatible; deeper runtime work (queues, schedule composition, observability depth, fiber runtime) is pulled
  forward only when a concrete application needs it.
- Whether Flow ships 1.0 simultaneously at its current surface or stays 0.x while the boundary stack goes stable is an
  open decision; packages currently share one coordinated version from `Directory.Build.props`.

## Adoption Driver

The concrete target shaping priorities is a real configuration system: ~100 config variants stored as flat records
with discriminator fields inspected at runtime to select which other fields apply, most fields nullable depending on
variant, produced by a wizard UI whose shape mutates over time, with breakage today caught by scrutiny rather than
systematically. Axial's answer, in dependency order:

1. **Model the variants honestly**: internally tagged unions (`Schema.inlineUnion`) for discriminator-beside-fields
   records, `Schema.option` for the genuinely optional remainder, `Schema.enum` for payload-less cases. Most
   "nullable" fields become required-within-their-variant.
2. **Version the boundary**: explicit `Config.vN` schemas with manual migrations, so reading any stored config is
   detect version → migrate forward → parse against one current schema with path-aware diagnostics
   (`Axial.Schema.Contract`, `src/Axial.Schema/Contract.fs`).
3. **Validate at write time**: generated JSON Schema lets the wizard reject broken configs before they enter storage.
4. **Generate at scale**: the `.contract` declaration grammar and generator make authoring ~100 versioned contracts
   tolerable. The generator
   emits ordinary checked-in F# builder code, so the grammar is never required reading for someone maintaining the
   codebase.

Within the contract thread the original order was versioning/migration machinery → grammar + generator; in
practice the grammar + generator shipped first (single-version, wire-tier scope), the versioning/migration
engine (`Contract<'model>`) followed on 2026-07-13, and multi-version generator support (whole version chains
from one `.contract` file, with migrations as typed parameters of a generated builder) plus the public
`docs/schema/contracts.md` guide shipped on 2026-07-16. Remaining order: dogfood on the real config system →
LSP informed by that experience.

## Positioning

The public story is a ladder, not three doors:

1. Plain `Result` with your own error DU — simple code.
2. Constructor-last `Schema.define` shapes — domain models; this is what newcomers learn and the only authoring surface.
3. `.contract` files and generation — for teams with many versioned boundaries. Positioning sentence: contracts
   generate the same Schema code you would write by hand; reading the generated file is understanding the system.

Contracts must never be presented as an entry point or a requirement. A team with three models and no version churn
should never encounter them. Docs follow the three-area, problem-first framing already in place.

## Product Requirements

1. Axial is .NET-first. Primary targets are .NET TFMs; JavaScript support means Fable-generated JavaScript; JVM and
   ZIO Native material is reference semantics only, never a platform commitment.
2. The small, idiomatic F# surface is preserved while capability expands. Existing workflows and computation
   expressions stay source-compatible unless a breaking change is explicitly accepted; Scala names are not copied
   mechanically where they hurt F# usage.
3. One declaration, many interpreters. Schema definitions stay independent of diagnostics, structured data, and flow
   execution; interpreters (parsing, validation, codec, JSON Schema, inspection) consume the same declaration. No
   interpreter duplicates another's lowering rules.
4. Reflection is never the foundation. The authored schema path stays AOT-, trimming-, and Fable-compatible;
   boilerplate relief comes from build-time generation over explicit schemas, not runtime discovery.
5. .NET, Fable, and AOT are separate compatibility tracks with explicit gates: .NET tests are the baseline, the
   supported Fable surface is compiled and tested (including `Axial.Codec` with a Node round-trip), and
   trimming/NativeAOT smoke coverage guards the .NET deployment surface.
6. Tests grow with features: cancellation, finalization, concurrent races, boundary diagnostics, codec round-trips,
   and migration paths each get focused suites; existing tests are regression anchors.

## Non-Goals

- No JVM target and no direct execution on ZIO Native.
- No full ZIO API parity; the external ZIO corpus is a behavior/naming reference lens only.
- No mechanically translated Scala API replacing the F#-friendly surface.
- No reflection-based schema construction, binding, validation, or codec execution.
- No second schema-authoring surface (the `schema create { }` CE stays rejected); the grammar generates the one
  existing surface.

## References

- Flow-group and platform backlog: `LATER_TODO.md` (kept demand-driven; references the external ZIO corpus at
  `/home/adam/projects/zio_fsflow_docs/zio` — reference PRD `specs/prd.md`, reference TODO `TODO.md`, source/test
  specs `specs/*.md`).
- The pre-schema, effects-only version of this PRD (the FsFlow delta PRD, including its source/test inventory and
  delta-against-ZIO analysis) is preserved in git history at commit `a8f7d906` and earlier.
