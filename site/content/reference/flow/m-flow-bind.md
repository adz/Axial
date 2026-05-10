---
title: "FsFlow.Flow.bind"
linkTitle: "bind`"
type: docs
---

Sequences a synchronous continuation after a successful value.

## Remarks

This is the "flatmap" operation for `Flow`. It allows for dependent
 steps where the second flow depends on the value produced by the first.


## Parameters

- `binder`: A function that takes the successful value and returns a new flow.
- `flow`: The source flow to sequence.

## Returns

A `Flow` representing the combined workflow.

