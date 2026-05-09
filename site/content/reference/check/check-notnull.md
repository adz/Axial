---
title: notNull
type: docs
---

Returns the value when it is not null.


```fsharp
let notNull (value: 'a when 'a : null) : Check<'a>
```




## Parameters

- `value`: The value to check.

## Returns

A `Check` containing the non-null value.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L773)

