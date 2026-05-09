---
title: ensureNotCanceled
type: docs
---

Checks if cancellation has been requested and returns a typed error if it has.


```fsharp
let ensureNotCanceled<'env, 'error> (canceledError: 'error) : AsyncFlow<'env, 'error, unit>
```


## Remarks

This observes the current token state and returns a typed error immediately instead of waiting for an exception.


## Parameters

- `canceledError`: The error to return if canceled.

## Information

- **Module**: `AsyncFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L399)

