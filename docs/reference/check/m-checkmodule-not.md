---
title: "FsFlow.CheckModule.not"
linkTitle: "not`"
---

Returns success when the supplied check fails.

## Remarks

This is a logical "not" operation for checks. Note that it discards the success value
 and returns `Unit` on success.


## Parameters

- `check`: The source `Check` to invert.

## Returns

A `Check` that succeeds if the input fails.

