---
title: catch
type: docs
---

Catches exceptions raised during execution and maps them to a typed error.


```fsharp
let catch (handler: exn -> 'error) (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value>
```


## Remarks

Exceptions that are not caught by this helper will bubble up to the caller of `run`.
This ensures that known exceptions can be handled within the flow context.


## Parameters

- `handler`: A function of type `exn -> 'error` to map the exception.
- `flow`: The source flow of type `Flow` to monitor.

## Returns

A `Flow` that converts exceptions into success-path errors.

## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L243)

