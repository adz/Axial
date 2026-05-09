---
title: Check.and
linkTitle: and
---

Returns success when both checks succeed.


```fsharp
let ``and`` (left: Check<'left>) (right: Check<'right>) : Check<unit>
```


## Remarks

This is a logical "and" operation. It short-circuits: if `left` fails,
`right` is not evaluated.


## Parameters

- `left`: The first check.
- `right`: The second check.

## Returns

A `Check` that succeeds only if both inputs succeed.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L490)

