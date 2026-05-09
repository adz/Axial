---
title: create
type: docs
---

Creates a runtime context from the supplied runtime services, environment, and cancellation token.


```fsharp
let create (runtime: 'runtime) (environment: 'env) (cancellationToken: CancellationToken) : RuntimeContext<'runtime, 'env>
```




## Parameters

- `runtime`: The runtime services of type `'runtime`.
- `environment`: The application environment of type `'env`.
- `cancellationToken`: The `CancellationToken`.

## Returns

A new `RuntimeContext`.

## Information

- **Module**: `RuntimeContext`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L39)

