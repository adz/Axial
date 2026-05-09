---
title: Check.failIfSome
linkTitle: failIfSome
---

Returns success when the option is `None`.


```fsharp
let failIfSome (opt: 'a option) : Check<unit>
```




## Parameters

- `opt`: The option to check.

## Returns

A `Check` that succeeds if the option is `None`.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L589)

