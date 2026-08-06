# Current Ideas

This folder is for active design sketches and pre-ideas that are not yet accepted architecture.

Keep files here short enough for coding agents to scan quickly. When an idea is accepted, move the rule into
`AGENTS.md` or `dev-docs/PLAN.md` and delete the detailed sketch. When an idea is rejected or superseded, delete it
rather than keeping a historical spec that no longer matches the codebase.

Active sketches:

- `refined-schema-proof.md` — unresolved exploration of refined schema proof and safe record updates, with a prototype
  under `prototypes/refined-private-record/`.
- `flow-gate-rename.md` — extracted proposal for Axial application gate naming, independent of the
  Parse/Constraint/Refinement/Schema work.
- `architecture-guardrails.md` — proposed adopter-facing architecture guidance and a staged tooling direction for
  schema laws, compile-negative proofs, project roles, and compiled-code/public-surface audits.
- `project-split.md` — proposal to separate two repositories: Schema (carrying Data, Result, and the Values packages
  plus all Schema satellites) and Flow (plus its satellites), with an independent release train and documentation
  entry point each.
- `format-and-json-runtime.md` — extracted from `project-split.md`: one package per representation format, and a
  shared schema-to-codec compiler over platform-specific .NET and Fable JSON runtimes.
- `retire-errorhandling.md` — delete the `Axial.ErrorHandling` meta-package, promote `Axial.Result` to a
  top-level docs entry point next to Data, and collapse Constraint, Refined, and Parse under a docs-only "Values"
  nav group. Phase 1 (expanding Result) and the package half of Phase 4 are implemented — both meta-packages are
  gone. The docs-tree move (Phases 2-3) is outstanding.
- `database.md` — direction sketch for a typed relational layer (generated immutable query AST interpreted through
  Flow, building on `Schema`; its older `FieldRef` references need redesign if the idea is promoted).
- `flow-stream-proving.md` — pre-1.0 plan to prove and freeze resource-safe, portable FlowStream semantics through
  Process and narrow TCP, Serial, WebSocket, and SSE slices.
- `flow-transport-packages.md` — package and API direction layered on the FlowStream proving plan for Transport,
  framing, Network, Serial, WebSocket, streaming HTTP/SSE, Compression, and later Process integration.

Implemented work and settled decisions do not remain in this folder. Constraint unification and contextual
constraint localization shipped and are recorded in `dev-docs/decisions/README.md` and `AGENTS.md`; the term
language, `FieldReference`, `Origin`, and `ConstraintExpression.Relational` were removed from that milestone
rather than deferred as placeholders, so they return only when a real consumer establishes field identity,
nesting, and proof semantics. Contract generation, versioning, and record-first
derivation outcomes are recorded in `dev-docs/decisions/README.md`; remaining consumer-gated schema work is in
`dev-docs/TASKS.md`. The implemented Flow comparison examples live in `examples/Axial.Comparisons`.
