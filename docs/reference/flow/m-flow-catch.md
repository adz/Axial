---
title: "FsFlow.Flow.catch"
linkTitle: "catch`"
---

Catches exceptions raised during execution and maps them to a typed error.

## Remarks

Exceptions that are not caught by this helper will bubble up to the caller of `run`.
 This ensures that known exceptions can be handled within the flow context.


## Parameters

- `handler`: A function of type `exn -> 'error` to map the exception.
- `flow`: The source flow of type `Flow` to monitor.

## Returns

A `Flow` that converts exceptions into success-path errors.

