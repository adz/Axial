# Current Ideas

This folder is for active design sketches and pre-ideas that are not yet accepted architecture.

Keep files here short enough for coding agents to scan quickly. When an idea is accepted, move the rule into
`AGENTS.md` or `dev-docs/PLAN.md` and delete the detailed sketch. When an idea is rejected or superseded, delete it
rather than keeping a historical spec that no longer matches the codebase.

Active sketches and status records (several are resolved/shipped but kept for their reasoning until the surface
settles — see each file's Status line):

- `schema-contract-versioning.md` — Contract sketch: versioned boundary schemas with manual typed migrations. The
  runtime engine is the active queue item (`dev-docs/TASKS.md` Phase 28).
- `contract-grammar.md` — the `.contract` grammar. Status: grammar library + generator IMPLEMENTED (single-version,
  wire tier); LSP and multi-version support pending.
- `schema-source-generation.md` — generation targets. The attribute route stays deferred; the declaration-first
  route shipped via the contract grammar; the optics/checked-constructor sections fed `FieldRef` and
  `Model.validate`.
- `model-construct.md` — Status: RESOLVED by `Model<'model>` + `Model.validate`; kept as the design-space map.
- `trusted-model-wrapper.md` — Status: SHIPPED as `Model<'model>`; kept for the reasoning record.
- `zio-schema-comparison.md` — the ZIO Schema deep dive; source of the Phase 29/30 gap ranking in
  `dev-docs/TASKS.md`.
- `flow-compelling-examples.md` — candidate ZIO/Axial Flow scenarios comparing ordinary implementations, typed effect
  implementations, guarantees, and remaining application responsibilities.
- `flow-process-scripting-prd.md` — Flow-group; owned by the Flow track.
