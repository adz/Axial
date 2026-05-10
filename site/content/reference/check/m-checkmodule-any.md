---
title: "FsFlow.CheckModule.any"
linkTitle: "any`"
type: docs
---

Returns success when at least one check in the sequence succeeds.

## Remarks

Sequentially evaluates each check in the `checks` sequence.
 Stops at the first success.


## Parameters

- `checks`: A sequence of checks.

## Returns

A `Check` that succeeds if any input succeeds.

