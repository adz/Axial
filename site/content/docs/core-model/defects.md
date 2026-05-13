---
weight: 25
title: Defects and Exceptions
description: Why FsFlow keeps a first-class defect branch alongside typed failures and interruption.
type: docs
---


This page shows why FsFlow keeps `Cause.Die` as a first-class outcome and how it differs from typed failures and cancellation.

FsFlow has three distinct failure kinds:

- `Cause.Fail error`: an expected domain failure
- `Cause.Interrupt`: a cancellation or interruption signal
- `Cause.Die exn`: an unexpected defect or bug

You can produce `Cause.Die` explicitly with `Flow.die`, or let the runtime preserve it when an uncaught exception escapes a `Flow`, `AsyncFlow`, or `TaskFlow` boundary.

Keeping those cases separate is not about adding ceremony. It is about keeping the failure story lossless inside the same execution model.

## Why Not Just Throw Exceptions?

Thrown exceptions are useful, but they are not the same as a typed defect channel.

If a flow throws, the exception escapes as control flow unless a caller catches it. At that point:

- the failure is no longer a value you can inspect or combine
- the runtime has already unwound the stack
- the code that handles the failure must remember to catch it

That works for local error handling, but it breaks down as soon as you want the failure story to stay inside the workflow algebra.

`Cause.Die` exists so FsFlow can keep defects as data until the caller chooses what to do with them.

## Why Keep Defects Separate From Typed Errors?

Typed errors answer a different question from defects.

- `Cause.Fail` means "the domain rejected this input or action"
- `Cause.Die` means "something went wrong that was not part of the domain contract"

That distinction matters when you are composing workflows:

- retries should usually target `Fail`, not `Die`
- fallback logic should usually target `Fail`, not `Die`
- logging and observability should record defects differently from expected domain errors

If defects are collapsed into ordinary errors, the runtime can no longer tell whether a failure was expected, retryable, or a bug.

## Why Keep Defects In The Same Algebra?

Keeping defects inside `Exit` and `Cause` gives FsFlow a lossless failure model.

That means a workflow can carry all of this through composition:

- success values
- typed failures
- interruption
- defects

The value-level representation lets the runtime and combinators preserve meaning instead of flattening everything into a single `exn`.

## What Users Should Do

Users should treat `Cause.Die` as the defect path, not as the normal error path.

The intended surface is:

- use `Flow.fail` or `Flow.error` for expected domain errors
- use interruption-aware helpers for cancellation
- use `Flow.die` for explicit defects
- use `Flow.catch` only when you intentionally want to convert an exception into a typed domain error

That keeps the code honest about intent.

## What The Runtime Should Do

The runtime should preserve defects as `Cause.Die` when it observes an unexpected exception at the execution boundary.

That gives the library two important properties:

- explicit defects remain explicit
- unexpected exceptions become visible inside the workflow outcome instead of disappearing as ambient control flow

Retry helpers and fallback combinators should continue to treat `Cause.Die` and `Cause.Interrupt` as terminal. Only `Cause.Fail` should participate in retry or domain fallback decisions.

## Why This Page Exists

This is the reason `Die` stays in the model:

- `Fail` is for domain control flow
- `Interrupt` is for cancellation
- `Die` is for defects

Those three cases are different enough to deserve separate branches.

## Next

Read [Execution Semantics](./semantics.md) for the full `Flow -> Effect -> Exit` story, or [Task and Async Interop](./task-async-interop.md) for how the builder binds different runtime shapes.
