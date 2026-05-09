---
title: or
---

Returns success when either check succeeds.


```fsharp
let ``or`` (left: Check<'left>) (right: Check<'right>) : Check<unit>
```


## Remarks

This is a logical "or" operation. It short-circuits: if `left` succeeds,
`right` is not evaluated.


## Parameters

- `left`: The first check.
- `right`: The second check.

## Returns

A `Check` that succeeds if either input succeeds.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L506)

