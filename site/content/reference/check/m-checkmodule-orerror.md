---
title: "FsFlow.CheckModule.orError"
linkTitle: "orError`"
type: docs
---

Maps a unit error into the supplied application error value.

## Remarks

This is the primary bridge from checks to domain-specific results.


## Parameters

- `error`: The domain error of type `'error` to return on failure.
- `result`: The source `Check`.

## Returns

A `Result` with the provided error value.

