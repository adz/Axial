---
title: fromPredicate
---

Builds a check from a predicate while preserving the successful value.


```fsharp
let fromPredicate (predicate: 'value -> bool) (value: 'value) : Check<'value>
```




## Parameters

- `predicate`: A function of type `'value -> bool` to test the value.
- `value`: The value of type `'value` to check.

## Returns

A `Check` containing the value if the predicate succeeds.

## Information

- **Module**: `Check`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L464)

