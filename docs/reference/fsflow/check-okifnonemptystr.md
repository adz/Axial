---
title: okIfNonEmptyStr
---

Returns the string when it is not null or empty.


```fsharp
let okIfNonEmptyStr (str: string) : Check<string>
```




## Parameters

- `str`: The string to check.

## Returns

A `Check` containing the non-empty string.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L713)

