---
title: "FsFlow.ValidationModule.traverseIndexed"
linkTitle: "traverseIndexed`"
---

Maps a sequence into validations while prefixing each item with its index.

## Remarks

This is the indexed version of `sequence`. It is useful for list and array
 validation because each item can keep its own `Index`
 branch without the caller manually wrapping every item.


## Parameters

- `binder`: A function of type `int -> 'source -> Validation&lt;'value, 'error&gt;`.
- `values`: The input sequence.

## Returns

A validation containing the list of values or accumulated diagnostics.

