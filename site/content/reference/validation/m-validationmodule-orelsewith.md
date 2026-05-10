---
title: "FsFlow.ValidationModule.orElseWith"
linkTitle: "orElseWith`"
type: docs
---

Computes a fallback validation from the source diagnostics when validation fails.

## Remarks

This is the lazy counterpart to `orElse` and is useful when the alternate
 branch depends on the accumulated diagnostics.


## Parameters

- `fallback`: A function that turns the diagnostics into an alternate validation.
- `validation`: The source validation.

## Returns

The source validation when it succeeds, otherwise the computed fallback validation.

