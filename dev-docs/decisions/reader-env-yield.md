# Reader-Env Yield

Status: withdrawn.
Recorded: 2026-04-29.

## Extracted From

- `dev-docs/TASKS.md`:
  - the `yield` ergonomics chat around reader environments

## Source Date

- 2026-04-29: `Migrate to Docusaurus and prepare 0.3.0 release`

## Decision

This direction was superseded by the explicit `Flow.read` API and the `Env` request token.

## Shape

- `Flow.read project` remains the canonical reader projection.
- `Env<'dep>` and `Env<'dep, 'value>` remain the request tokens for capability-style reads.

## Why

- The `yield` shorthand duplicated `Flow.read` without adding enough clarity.
- `Env` already covers the capability-request story with less ambiguity.

## Caveats

- The removed shorthand made the CE surface larger without improving the core model.
- `yield` also conflicted with the usual reader/sequence intuition.

## Consequences

- Keep the builder surface centered on `return`, `return!`, `let!`, and `Env`.
- Keep `Flow.read` as the canonical explicit entry point.
