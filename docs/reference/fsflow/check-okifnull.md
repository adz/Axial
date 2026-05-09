---
title: okIfNull
---

Returns success when the value is null.


```fsharp
let okIfNull (value: 'a when 'a : null) : Check<unit>
```




## Parameters

- `value`: The value to check.

## Returns

A `Check` that succeeds if the value is null.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L643)

