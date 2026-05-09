---
title: Check.failIfValueSome
linkTitle: failIfValueSome
---

Returns success when the value option is `ValueNone`.


```fsharp
let failIfValueSome (opt: 'a voption) : Check<unit>
```




## Parameters

- `opt`: The value option to check.

## Returns

A `Check` that succeeds if the value option is `ValueNone`.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L621)

