---
title: okIfEmptyStr
---

Returns success when the string is null or empty.


```fsharp
let okIfEmptyStr (str: string) : Check<unit>
```




## Parameters

- `str`: The string to check.

## Returns

A `Check` that succeeds if the string is null or empty.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L719)

