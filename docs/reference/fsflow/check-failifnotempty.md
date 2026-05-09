---
title: Check.failIfNotEmpty
linkTitle: failIfNotEmpty
---

Returns success when the sequence is empty.


```fsharp
let failIfNotEmpty (coll: seq<'a>) : Check<unit>
```




## Parameters

- `coll`: The sequence to check.

## Returns

A `Check` that succeeds if the sequence is empty.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L673)

