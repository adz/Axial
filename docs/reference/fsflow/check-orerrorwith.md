---
title: orErrorWith
---

Maps a unit error into an application error produced on demand.


```fsharp
let orErrorWith (errorFn: unit -> 'error) (result: Check<'value>) : Result<'value, 'error>
```




## Parameters

- `errorFn`: A function of type `unit -> 'error` to produce the error.
- `result`: The source `Check`.

## Returns

A `Result` with the produced error value.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L812)

