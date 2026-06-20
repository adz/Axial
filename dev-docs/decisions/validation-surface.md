# Validation Surface

## Extracted From

- `dev-docs/PLAN.md`
- `dev-docs/TASKS.md`

## Source Date

- 2026-05-03: validation graph and `Check` surface fully implemented

## Decision

Axial uses a three-layer validation model:

- `Check<'value>` is the reusable predicate layer and carries a `unit` failure placeholder
- `Result<'value, 'error>` is the fail-fast carrier for typed failures
- `Validation<'value, 'error>` is the accumulating carrier and stores structured `Diagnostics<'error>`

The graph type is explicit and path-aware:

- `Diagnostic<'error>` represents one failure at one path
- `Diagnostics<'error>` stores local diagnostics plus child branches
- `Diagnostics.merge` combines graphs recursively
- `Diagnostics.flatten` turns the graph into a flat list for display or tests

The canonical combinators are:

- `Check.negate`, `Check.both`, `Check.either`, `Check.all`, and `Check.any`
- `Check.when*` for value-preserving checks and `Check.take*` for extracted or reshaped success values such as `Check.whenNotBlank` and `Check.takeSome`
- `Check.withError` for bridging unit-error checks into typed errors
- `Validation.fromResult`, `Validation.map2`, `Validation.apply`, `Validation.collect`, and `Validation.sequence`
- `validate {}` as the applicative accumulation entry point

The legacy `Validate` module remains only as a compatibility alias.

## Why

- The separate names make the semantics visible at the type and module level.
- A graph carries structure that a flat error list would lose.
- `Check` stays focused on reusable predicates instead of becoming a pseudo-validation monad.
- `Validation` keeps applicative accumulation explicit instead of hiding it behind `Result`.

## Consequences

- Narrative docs should lead with `Check`, `Diagnostics`, `Validation`, and `validate {}`.
- `Validate` should be documented as compatibility terminology, not as the primary surface.
- API pages should link directly to the source definitions for the graph types and helpers.
- Future validation work should extend the graph model instead of reintroducing a flat-list abstraction as the primary story.
