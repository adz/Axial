---
title: not
---

Returns success when the supplied check fails.


```fsharp
let not (check: Check<'value>) : Check<unit>
```


## Remarks

This is a logical "not" operation for checks. Note that it discards the success value
and returns `Unit` on success.


## Parameters

- `check`: The source `Check` to invert.

## Returns

A `Check` that succeeds if the input fails.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L477)

