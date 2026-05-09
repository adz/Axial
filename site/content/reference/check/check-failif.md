---
title: failIf
type: docs
---

Returns success when the condition is false.


```fsharp
let failIf (cond: bool) : Check<unit>
```




## Parameters

- `cond`: The boolean condition to check.

## Returns

A `Check` that succeeds if `cond` is false.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L567)

