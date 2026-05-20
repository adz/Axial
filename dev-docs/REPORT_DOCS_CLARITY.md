# Superseded Documentation Clarity Report

This report is retained as historical context only.

It has been superseded by `dev-docs/PLAN.md` and the current public docs under `docs/managing-dependencies/`.

The active clarification is:

- business dependencies belong in explicit `'env`
- operational services belong in the ambient runtime
- records plus `Flow.read` are the default app dependency pattern
- `IHas<'T>` plus `Flow.service` is an optional strict app contract pattern
- `Flow.inject` is .NET host-edge interop
- registry-backed runtime and public `RuntimeContext<'runtime, 'env>` are not current user-facing architecture

Do not use the original recommendations in this report as live tasks.
