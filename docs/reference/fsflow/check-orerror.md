---
title: Check.orError
linkTitle: orError
---

Maps a unit error into the supplied application error value.


```fsharp
let orError (error: 'error) (result: Check<'value>) : Result<'value, 'error>
```


## Remarks

This is the primary bridge from checks to domain-specific results.


## Parameters

- `error`: The domain error of type `'error` to return on failure.
- `result`: The source `Check`.

## Returns

A `Result` with the provided error value.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L803)

