# Current Ideas

This folder is for active design sketches and pre-ideas that are not yet accepted architecture.

Keep files here short enough for coding agents to scan quickly. When an idea is accepted, move the rule into
`AGENTS.md` or `dev-docs/PLAN.md` and delete the detailed sketch. When an idea is rejected or superseded, delete it
rather than keeping a historical spec that no longer matches the codebase.

Active sketches:

- `architecture-guardrails.md` — proposed adopter-facing architecture guidance and a staged tooling direction for
  schema laws, compile-negative proofs, project roles, and compiled-code/public-surface audits.
- `project-split.md` — proposal to separate Schema/ErrorHandling and Flow repositories, keep formats in separate Schema
  packages, split the documentation experience, and isolate .NET/Fable codec runtimes behind
  concentrated compiler directives.
- `database.md` — direction sketch for a typed relational layer (generated immutable query AST interpreted through
  Flow, building on `Schema`; its older `FieldRef` references need redesign if the idea is promoted).
- `flow-stream-proving.md` — pre-1.0 plan to prove and freeze resource-safe, portable FlowStream semantics through
  Process and narrow TCP, Serial, WebSocket, and SSE slices.
- `flow-transport-packages.md` — package and API direction layered on the FlowStream proving plan for Transport,
  framing, Network, Serial, WebSocket, streaming HTTP/SSE, Compression, and later Process integration.

Implemented work and settled decisions do not remain in this folder. Contract generation, versioning, and record-first
derivation outcomes are recorded in `dev-docs/decisions/README.md`; remaining consumer-gated schema work is in
`dev-docs/TASKS.md`. The implemented Flow comparison examples live in `examples/Axial.Flow.Comparisons`.
