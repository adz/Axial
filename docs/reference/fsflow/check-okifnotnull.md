---
title: okIfNotNull
---

Returns the value when it is not null.


```fsharp
let okIfNotNull (value: 'a when 'a : null) : Check<'a>
```




## Parameters

- `value`: The value of type `'a` to check for null.

## Returns

A `Check` containing the non-null value.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L637)

