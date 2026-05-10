---
title: "FsFlow.ValidationModule.orElse"
linkTitle: "orElse`"
type: docs
---

Falls back to another validation when the source validation fails.

## Remarks

This is a left-biased choice operator. If the source succeeds, the fallback is not used.
 If the source fails, the fallback validation is returned as-is.


## Parameters

- `fallback`: The validation to use when the source fails.
- `validation`: The source validation.

## Returns

The source validation when it succeeds, otherwise the fallback validation.

