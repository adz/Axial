---
title: "FsFlow.Validation"
linkTitle: "Validation"
---

An accumulating validation result that keeps the structured diagnostics graph visible.

## Remarks

Unlike `FSharpResult`, this type is designed for applicative
 composition using `and!` in the `validate { }` builder, which merges errors instead of
 short-circuiting.


