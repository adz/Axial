# Decision Summary

This folder keeps only high-level durable decisions. Detailed historical specs are deleted once their useful rules have
been folded into `AGENTS.md`, `dev-docs/PLAN.md`, or this summary.

## Current Invariants

- `Flow<'env, 'error, 'value>` is the public workflow model. Platform carriers are execution/adaptation boundaries, not
  user-facing workflow types.
- `Axial.Flow`, `Axial.ErrorHandling`, `Axial.Refined`, and `Axial.Validation` are independent leaf packages. The
  umbrella `Axial` package may reference them, but leaf packages must not depend on each other.
- Explicit dependencies live in `'env`. The ambient runtime is reserved for closed executor mechanics such as
  cancellation, scope, scheduling, interruption, and trace metadata.
- Operational services are explicit services provisioned through records, nominal `IHas<'service>` contracts, host-edge
  `IServiceProvider` resolution, and `Layer`.
- `Check` and `Result` helpers belong to `Axial.ErrorHandling`; `Parse`, `Refine`, and the `refine { }` builder belong
  to `Axial.Refined`; `Validation` and `Diagnostics` belong to `Axial.Validation`; `Policy`, `Bind`, and `BindError`
  belong to `Axial.Flow`.
- `Check` is a complete typed value-constraint subsystem:
  `Check<'value> = 'value -> Result<unit, CheckFailure list>`. Checks are path-free, raw-input-free value programs;
  value-preserving guards and extraction helpers belong in `Result`, and parsing and refined value construction belong in
  `Axial.Refined`.
- `Result` keeps fail-fast adapters around `Check`, not a second accumulating constraint language. The retained helper
  families are:
  - generic Result combinators and conversions (`ok`, `error`, `map`, `bind`, `mapError`, `withError`, `fromTry`,
    `fromChoice`, `toOption`, `toValueOption`, and `defaultValue`);
  - extraction helpers for option, value option, nullable, result, and sequence values (`someOr`, `noneOr`,
    `valueSomeOr`, `valueNoneOr`, `nullableOr`, `notNullOr`, `okOr`, `errorOr`, `headOr`, `single`, and `atMostOne`);
  - value-preserving fail-fast guards that mirror executable `Check` programs (`keepIf` today; `Result.require` and
    `Result.guard` when the API is aligned; string length, ordered range, and sequence count guards).
  Do not add new predicate-specific `Result` helpers when the same rule belongs in `Check.*` and can be adapted through
  the generic fail-fast guard.
- First-pass ordered range checks stay in generic `Check.Number` helpers over comparable values. Do not add separate
  `Check.Int`, `Check.Decimal`, `Check.Float`, or date/time check modules until a schema, refined value, or diagnostics
  requirement needs type-specific semantics beyond plain ordering.
- `Axial.Schema` starts as its own package and project as soon as schema source work begins. Do not incubate schema
  definitions inside `Axial.Validation`; keep schema definitions independent and put input, validation, diagnostics, and
  rules integration in `Axial.Validation.Schema`.
- `Bind` is only for assigning or mapping a source error immediately before `flow { }` binds it. In pure code, use
  `Result.require`, `Result.mapError`, or `Validation.mapError`.
- Generated reference docs come from XML comments and generator inputs. Do not hand-edit generated reference pages as the
  primary source of truth.

## Open Ideas

Pre-ideas and proposals live in [`../current-ideas/`](../current-ideas/). When accepted, keep only the durable rule here
or in `AGENTS.md`, then delete the detailed sketch.
