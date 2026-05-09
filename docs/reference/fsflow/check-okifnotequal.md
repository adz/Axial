---
title: okIfNotEqual
---

Returns success when the values are not equal.


```fsharp
let okIfNotEqual (expected: 'a) (actual: 'a) : Check<unit>
```




## Parameters

- `expected`: The expected value.
- `actual`: The actual value.

## Returns

A `Check` that succeeds if the values differ.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L693)

