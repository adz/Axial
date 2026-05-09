---
title: okIf
---

Returns success when the condition is true.


```fsharp
let okIf (cond: bool) : Check<unit>
```




## Parameters

- `cond`: The boolean condition to check.

## Returns

A `Check` that succeeds if `cond` is true.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L561)

