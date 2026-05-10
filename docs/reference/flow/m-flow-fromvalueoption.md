---
title: "FsFlow.Flow.fromValueOption"
linkTitle: "fromValueOption`"
---

Lifts a value option into a synchronous flow with the supplied error.



## Parameters

- `error`: The error to return if the value option is `ValueNone`.
- `value`: The value option to lift.

## Returns

A `Flow` that succeeds with the option's value or fails with the provided error.

