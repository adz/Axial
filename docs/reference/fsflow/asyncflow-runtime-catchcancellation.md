---
title: AsyncFlow.Runtime.catchCancellation
linkTitle: catchCancellation
---

Catches `OperationCanceledException` and converts it into a typed error.


```fsharp
let catchCancellation (handler: OperationCanceledException -> 'error) (flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value>
```


## Remarks

This translates cancellation exceptions raised during execution. It does not pre-check the token.


## Parameters

- `handler`: The function to convert the exception.
- `flow`: The source flow.

## Information

- **Module**: `AsyncFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L382)

