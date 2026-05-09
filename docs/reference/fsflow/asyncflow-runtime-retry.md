---
title: AsyncFlow.Runtime.retry
linkTitle: retry
---

Retries a flow according to the specified policy.


```fsharp
let retry (policy: RetryPolicy<'error>) (flow: AsyncFlow<'env, 'error, 'value>) : AsyncFlow<'env, 'error, 'value>
```




## Parameters

- `policy`: The retry policy.
- `flow`: The flow to retry.

## Information

- **Module**: `AsyncFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L585)

