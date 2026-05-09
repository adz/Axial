---
title: okIfSome
type: docs
---

Returns the value when the option is `Some`.


```fsharp
let okIfSome (opt: 'a option) : Check<'a>
```




## Parameters

- `opt`: The `FSharpOption` to check.

## Returns

A `Check` containing the value if present.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L573)

