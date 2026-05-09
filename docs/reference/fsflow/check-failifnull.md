---
title: Check.failIfNull
linkTitle: failIfNull
---

Returns the value when it is null.


```fsharp
let failIfNull (value: 'a when 'a : null) : Check<'a>
```




## Parameters

- `value`: The value to check.

## Returns

A `Check` containing the null value.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L655)

