---
title: all
type: docs
---

Returns success when every check in the sequence succeeds.


```fsharp
let all (checks: seq<Check<'value>>) : Check<unit>
```


## Remarks

Sequentially evaluates each check in the `checks` sequence.
Stops at the first failure.


## Parameters

- `checks`: A sequence of checks.

## Returns

A `Check` that succeeds only if all inputs succeed.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L521)

