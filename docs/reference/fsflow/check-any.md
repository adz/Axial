---
title: any
---

Returns success when at least one check in the sequence succeeds.


```fsharp
let any (checks: seq<Check<'value>>) : Check<unit>
```


## Remarks

Sequentially evaluates each check in the `checks` sequence.
Stops at the first success.


## Parameters

- `checks`: A sequence of checks.

## Returns

A `Check` that succeeds if any input succeeds.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L543)

