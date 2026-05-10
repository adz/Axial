---
title: "FsFlow.CheckModule.all"
linkTitle: "all`"
---

Returns success when every check in the sequence succeeds.

## Remarks

Sequentially evaluates each check in the `checks` sequence.
 Stops at the first failure.


## Parameters

- `checks`: A sequence of checks.

## Returns

A `Check` that succeeds only if all inputs succeed.

