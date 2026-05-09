---
title: Validation.collect
linkTitle: collect
---

Collects a sequence of validations into a single validation of a list.


```fsharp
let collect (validations: seq<Validation<'value, 'error>>) : Validation<'value list, 'error>
```


## Remarks

This operation is applicative: it will collect errors from ALL items in the sequence.


## Parameters

- `validations`: A sequence of type `seq&lt;Validation&lt;'value, 'error&gt;&gt;`.

## Returns

A validation containing the list of values or accumulated diagnostics.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L367)

