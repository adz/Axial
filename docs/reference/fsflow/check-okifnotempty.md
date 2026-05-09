---
title: Check.okIfNotEmpty
linkTitle: okIfNotEmpty
---

Returns the sequence when it is not empty.


```fsharp
let okIfNotEmpty (coll: seq<'a>) : Check<seq<'a>>
```




## Parameters

- `coll`: The sequence of type `seq&lt;'a&gt;` to check.

## Returns

A `Check` containing the non-empty sequence.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L661)

