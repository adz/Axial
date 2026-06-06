# Decision Logs

This folder captures settled design decisions and supporting rationale that no longer belong in `dev-docs/PLAN.md`.

For service, runtime, scope, and layer direction, prefer [`../scope-layer-redesign.md`](../scope-layer-redesign.md)
and the current [`../PLAN.md`](../PLAN.md). Older service/runtime research under `dev-docs/` and
`dev-docs/deprecated/caps-research/` may be historically useful but should not override the current redesign.

## Index

- [Unified Flow and Hybrid Interop Optimization](unified-flow-optimization.md): 2026-05-17. Integration of sync/async/task into a single `Flow` type with optimized inlined overloads.
- [Benchmark history](benchmark-history.md): 2026-04-28. Recorded benchmark runs and the conclusions they supported.
- [Docs source extraction](docs-source-extraction.md): 2026-04-29. Move API pages from hand-authored lifted markdown to a real source-doc extraction pass with source links.
- [Option and ValueOption binding](option-valueoption-binding.md): 2026-05-03. Keep implicit binding only for `unit` error workflows and use explicit conversion helpers for typed errors.
- [Logging ergonomics](logging-ergonomics.md): 2026-05-03. Keep the core logging abstraction generic and treat `ILogger` as an integration adapter.
- [Validation surface](validation-surface.md): 2026-05-03. `Check`, `Diagnostics`, `Validation`, and the applicative `validate {}` split.
- [Check surface redesign](check-surface-redesign.md): 2026-06-05, updated 2026-06-07. Unified `Check` predicate/when/take helpers and flow bind-site `BindError`.
- [Validation path scoping](validation-path-scoping.md): 2026-05-06. Root-local `validate {}` plus scoped helpers for `Key` / `Index` / `Name` branches.
- [API reference page shape](reference-page-shape.md): 2026-05-03. One page per public API surface, side-menu entries, examples, and versioned source links.
- [Scope, services, and layers](../scope-layer-redesign.md): 2026-06-02. Delete the registry, model operational services explicitly, and make Scope/Layer part of the v1 architecture.

## Deprecated

Decisions that have been superseded by newer architectural shifts are located in the [deprecated/](./deprecated/) folder.
