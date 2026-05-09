---
title: okIfBlank
---

Returns success when the string is blank.


```fsharp
let okIfBlank (str: string) : Check<unit>
```




## Parameters

- `str`: The string to check.

## Returns

A `Check` that succeeds if the string is null, empty, or whitespace.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L749)

