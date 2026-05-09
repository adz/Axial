---
title: timeout
---

Wraps a flow with a timeout. If the flow does not complete within the specified duration, returns a typed error.


```fsharp
let timeout (after: TimeSpan) (timeoutError: 'error) (flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value>
```


## Remarks

This helper translates timeout into a typed error. It does not automatically cancel the underlying work on timeout.


## Parameters

- `after`: The duration after which to timeout.
- `timeoutError`: The error to return on timeout.
- `flow`: The flow to wrap.

## Information

- **Module**: `AsyncFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L501)

