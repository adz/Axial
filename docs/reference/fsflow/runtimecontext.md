---
title: RuntimeContext
linkTitle: RuntimeContext
---

Captures the two-context shape of a task workflow execution:
runtime services, application capabilities, and the cancellation token for the current run.


```fsharp
type RuntimeContext<'runtime, 'env>
```


## Remarks

This type is the standard environment carrier for `TaskFlow`.
It separates low-level operational concerns (Runtime) from high-level domain dependencies (Environment).


## Information

- **Module**: Global
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Runtime.fs#L18)

