---
title: failIfValueNone
type: docs
---

Returns the value when the value option is `ValueSome`.


```fsharp
let failIfValueNone (opt: 'a voption) : Check<'a>
```




## Parameters

- `opt`: The value option to check.

## Returns

A `Check` containing the value if present.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L629)

