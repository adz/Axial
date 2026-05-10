---
title: "FsFlow.CheckModule.fromPredicate"
linkTitle: "fromPredicate`"
type: docs
---

Builds a check from a predicate while preserving the successful value.



## Parameters

- `predicate`: A function of type `'value -> bool` to test the value.
- `value`: The value of type `'value` to check.

## Returns

A `Check` containing the value if the predicate succeeds.

