---
title: "FsFlow.CheckModule.and"
linkTitle: "and`"
---

Returns success when both checks succeed.

## Remarks

This is a logical "and" operation. It short-circuits: if `left` fails,
 `right` is not evaluated.


## Parameters

- `left`: The first check.
- `right`: The second check.

## Returns

A `Check` that succeeds only if both inputs succeed.

