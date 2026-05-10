---
title: "FsFlow.CheckModule.or"
linkTitle: "or`"
---

Returns success when either check succeeds.

## Remarks

This is a logical "or" operation. It short-circuits: if `left` succeeds,
 `right` is not evaluated.


## Parameters

- `left`: The first check.
- `right`: The second check.

## Returns

A `Check` that succeeds if either input succeeds.

