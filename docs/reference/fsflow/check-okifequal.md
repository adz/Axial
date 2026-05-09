---
title: Check.okIfEqual
linkTitle: okIfEqual
---

Returns success when the values are equal.


```fsharp
let okIfEqual (expected: 'a) (actual: 'a) : Check<unit>
```




## Parameters

- `expected`: The expected value.
- `actual`: The actual value.

## Returns

A `Check` that succeeds if the values are equal.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L686)

