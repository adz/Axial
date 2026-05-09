---
title: failIfEmpty
type: docs
---

Returns the sequence when it is not empty.


```fsharp
let failIfEmpty (coll: seq<'a>) : Check<seq<'a>>
```




## Parameters

- `coll`: The sequence to check.

## Returns

A `Check` containing the non-empty sequence.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L679)

