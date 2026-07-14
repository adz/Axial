# Current Ideas

This folder is for active design sketches and pre-ideas that are not yet accepted architecture.

Keep files here short enough for coding agents to scan quickly. When an idea is accepted, move the rule into
`AGENTS.md` or `dev-docs/PLAN.md` and delete the detailed sketch. When an idea is rejected or superseded, delete it
rather than keeping a historical spec that no longer matches the codebase.

Active sketches:

- `contract-grammar.md` — the `.contract` grammar. Status: grammar library + generator IMPLEMENTED (single-version,
  wire tier); LSP and multi-version support pending.
- `schema-source-generation.md` — generation targets. The attribute route stays deferred; the declaration-first
  route shipped via the contract grammar; the optics/checked-constructor sections fed `FieldRef` and
  `Model.validate`.
- `zio-schema-comparison.md` — the ZIO Schema deep dive; source of the Phase 29/30 gap ranking in
  `dev-docs/TASKS.md`.
- `flow-compelling-examples.md` — candidate ZIO/Axial Flow scenarios comparing ordinary implementations, typed effect
  implementations, guarantees, and remaining application responsibilities.
- `database.md` — direction sketch for a typed relational layer (generated immutable query AST interpreted through
  Flow, building on `Schema`, `Model<'t>`, and `FieldRef`).
