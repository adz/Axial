---
title: sleep
type: docs
---

Suspends the flow for the specified duration, observing cancellation.


```fsharp
let sleep<'env, 'error> (delay: TimeSpan) : AsyncFlow<'env, 'error, unit>
```


## Remarks

If the runtime token is canceled, the underlying task raises cancellation which can be translated with `catchCancellation`.


## Parameters

- `delay`: The duration to sleep.

## Information

- **Module**: `AsyncFlow.Runtime`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L415)

