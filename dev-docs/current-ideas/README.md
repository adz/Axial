# Current Ideas

This folder is for active design sketches and pre-ideas that are not yet accepted architecture.

Keep files here short enough for coding agents to scan quickly. When an idea is accepted, move the rule into
`AGENTS.md` or `dev-docs/PLAN.md` and delete the detailed sketch. When an idea is rejected or superseded, delete it
rather than keeping a historical spec that no longer matches the codebase.

Active sketches:

- `schema-contract-versioning.md` — Contract sketch: versioned boundary schemas with manual typed migrations for
  config files, messages, events, and eventually database records.
- `contract-grammar.md` — companion grammar sketch for `.contract` declarations (JSON Schema coverage, five design
  rules, generator + LSP plan). Actively being worked; nothing accepted.
- `schema-source-generation.md` — pinned `[<Schema>]` generation target and deferral criteria for source generation.
