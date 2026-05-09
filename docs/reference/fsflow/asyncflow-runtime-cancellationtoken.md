---
title: AsyncFlow.Runtime.cancellationToken
linkTitle: cancellationToken
---

Reads the current cancellation token from the flow.


```fsharp
let cancellationToken<'env, 'error> : AsyncFlow<'env, 'error, CancellationToken>
```


## Remarks

This observes the runtime token; it does not translate cancellation into a typed error by itself.


## Information

- **Module**: `AsyncFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L369)

