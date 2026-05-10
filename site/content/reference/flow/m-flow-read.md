---
title: "FsFlow.Flow.read"
linkTitle: "read`"
type: docs
---

Projects a value from the current environment.

## Remarks

This is the primary way to access dependencies or configuration stored in the environment.
 The `projection` function is applied to the environment at execution time.


## Parameters

- `projection`: A function that extracts a value from the environment.

## Returns

A `Flow` containing the projected value.

