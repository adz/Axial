---
title: failIfEmptyStr
type: docs
---

Returns the string when it is null or empty.


```fsharp
let failIfEmptyStr (str: string) : Check<string>
```




## Parameters

- `str`: The string to check.

## Returns

A `Check` containing the empty or null string.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L731)

