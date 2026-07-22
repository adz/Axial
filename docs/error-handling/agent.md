---
title: For AI agents
description: High-signal Validation guidance for coding agents.
weight: 100
---

# For AI agents

Use this section for `Axial.ErrorHandling` and its focused `Axial.Result`, `Axial.Diagnostics`, and `Axial.Refined`
packages. They do not require Schema or Flow.

- Prefer `CheckDSL` for modules containing several reusable checks.
- Use a caller-owned error union at public application boundaries.
- Use `orError` when one application error replaces check details, and `mapError` when those details matter.
- Use `result {}` for dependent fail-fast steps and `validate {}` with sibling `and!` bindings for independent checks.
- Use `Diagnostics` when errors need paths, indexes, or names.
- Use refined types when later code must rely on a value-level rule without checking it again.

For compact prompt context, load [`/error-handling/llms.txt`](/error-handling/llms.txt).
