# Axial Tasks

This file is the active development queue. Keep completed work out of this file.
Keep live architecture direction in `dev-docs/PLAN.md`.
Keep speculative design sketches in `dev-docs/current-ideas/` and high-level durable decisions in
`dev-docs/decisions/`.

## Priority Order

Work this queue from top to bottom.

No active scope/layer redesign tasks remain.

Future service-package design work has moved to the root `TODO.md` because it is broader v1/post-v1 product work, not
part of this completed redesign queue.

## Active Surface Work

- Split tests into `Axial.Flow.Tests`, `Axial.ErrorHandling.Tests`, `Axial.Refined.Tests`, and `Axial.Validation.Tests`, with each test project
  building only against its own target package boundary.

- Decide whether the canonical validation bridge is `Validation.fromResult` or `Validation.ofResult`, then align source
  comments, docs, examples, and generated reference pages.

- Refresh `dev-docs/API_BASELINE.md` after package-boundary test projects exist.

- Finish the remaining reference cleanup in `dev-docs/REFERENCE_AUDIT.md`.

- Promote, reject, or delete each sketch in `dev-docs/current-ideas/` before implementation work starts.

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
