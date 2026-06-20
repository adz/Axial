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

- Execute the Axial split classification pass in `dev-docs/project-split.md`: assign each public file in `src/FsFlow` to `Axial.Flow`, `Axial.Result`, `Axial.Validation`, or `defer`, and record the decision explicitly.

- Remove the current forbidden package coupling from core code by making `BindError` independent of `Check.withError`, so `Axial.Flow` can remain package-independent.

- Create the three independent projects `Axial.Flow`, `Axial.Result`, and `Axial.Validation` with no Axial project references between them, and make them compile before retargeting downstream packages.

- Move fail-fast pure code into `Axial.Result`, keeping `result { }` there and not in `Axial.Validation`.

- Move accumulating validation and diagnostics code into `Axial.Validation`, keeping `validate { }` there and exposing `Validation.toResult` / `Validation.ofResult` as the canonical bridges through standard F# `Result`.

- Move effect/runtime orchestration code into `Axial.Flow`, keeping direct `Result` binding in `flow { }` and explicitly not adding direct `Validation` binding.

- Split tests into `Axial.Flow.Tests`, `Axial.Result.Tests`, and `Axial.Validation.Tests`, with each test project building only against its own target package boundary.

- After the new projects and tests are stable, retarget service packages, examples, and docs to the Axial package structure and regenerate generated documentation.

- Implement the `BindError` surface update and doc sweep that follow the split, keeping `Check` in `Axial.Result` and ensuring the public golden path no longer teaches the retired `Guard` or `Take` APIs.

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
