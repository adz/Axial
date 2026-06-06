# FsFlow Tasks

This file is the active development queue. Keep completed work out of this file.
Keep live architecture direction in `dev-docs/PLAN.md`.
Keep historical research in `dev-docs/deprecated/caps-research/` and settled decisions in `dev-docs/decisions/`.

## Priority Order

Work this queue from top to bottom.

No active scope/layer redesign tasks remain.

Future service-package design work has moved to the root `TODO.md` because it is broader v1/post-v1 product work, not
part of this completed redesign queue.

## Active Surface Work

- Implement the `Check`, `Take`, and `BindError` surface redesign, then sweep generated and hand-written docs so the public golden path no longer teaches the retired `Guard` API.

## Acceptance Checks

The current architecture is coherent when the following are true:

- public docs describe services as explicit and the runtime as executor-only
- user-facing workflow signatures show real service requirements in `'env`
- app/domain dependency examples start with records and `Flow.read`
- reusable service examples use `Service<'service>.get()`
- host-edge examples use `Service<'service>.resolve()` or provider-backed layers
- `Layer` is the documented provisioning mechanism
- registry-backed runtime is gone from both code and docs
- generated reference docs match source comments
