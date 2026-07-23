---
title: For AI agents
description: High-signal Validation guidance for coding agents.
weight: 100
type: docs
---


Use this section for `Axial.ErrorHandling` and its focused `Axial.Result`, `Axial.Diagnostics`, and `Axial.Refined`
packages. They do not require Schema or Flow.

- Prefer `CheckDSL` for modules containing several reusable checks.
- Define your own error union at public application boundaries.
- Use `orError` when one application error replaces check details, and `mapError` when those details matter.
- Use `result {}` for dependent fail-fast steps and `validate {}` with sibling `and!` bindings for independent checks.
- Use `Diagnostics` when errors need paths, indexes, or names.
- Use refined types when later code must rely on a value-level rule without checking it again.
- `Refine.from` runs the type-directed parse or refinement for one source and destination pair. Put the destination in the result annotation:
  `let id : Result<int, RefinementError> = Refine.from rawId`.
- In `refine {}`, bind raw input directly and put the parsed or refined target type on the left of `let!`, for example
  `let! (id: int) = rawId` or `let! (id: NonZeroInt) = parsedId`. Call `Parse.*` or `Refine.*` explicitly only when
  the operation needs information the target type does not carry.
- Your own destination type participates by defining `static member RefineFrom(raw, _: Destination)` returning
  `Result<Destination, RefinementError>`. Two interpretations for the same source and destination require different names.

For compact prompt context, load [`/error-handling/llms.txt`](/error-handling/llms.txt).
