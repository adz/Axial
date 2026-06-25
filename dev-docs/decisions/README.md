# Decision Summary

This folder keeps only high-level durable decisions. Detailed historical specs are deleted once their useful rules have
been folded into `AGENTS.md`, `dev-docs/PLAN.md`, or this summary.

## Current Invariants

- `Flow<'env, 'error, 'value>` is the public workflow model. Platform carriers are execution/adaptation boundaries, not
  user-facing workflow types.
- `Axial.Flow`, `Axial.Result`, and `Axial.Validation` are independent leaf packages. The umbrella `Axial` package may
  reference them, but leaf packages must not depend on each other.
- Explicit dependencies live in `'env`. The ambient runtime is reserved for closed executor mechanics such as
  cancellation, scope, scheduling, interruption, and trace metadata.
- Operational services are explicit services provisioned through records, nominal `IHas<'service>` contracts, host-edge
  `IServiceProvider` resolution, and `Layer`.
- `Check` belongs to `Axial.Result`; `Validation` and `Diagnostics` belong to `Axial.Validation`; `BindError` belongs to
  `Axial.Flow`.
- `Check` naming follows one grammar: unprefixed helpers test and usually return `unit`; `when*` helpers preserve the
  input; `take*` helpers extract or reshape the success value.
- `BindError` is only for assigning or mapping a source error immediately before `flow { }` binds it. In pure code, use
  `Check.withError`, `Result.mapError`, or `Validation.mapError`.
- Generated reference docs come from XML comments and generator inputs. Do not hand-edit generated reference pages as the
  primary source of truth.

## Open Ideas

Pre-ideas and proposals live in [`../current-ideas/`](../current-ideas/). When accepted, keep only the durable rule here
or in `AGENTS.md`, then delete the detailed sketch.
