---
title: Check
---

A reusable predicate result that either preserves a value on success or acts as a gate with
`unit` on success, while carrying a unit failure placeholder until the caller maps it into
a domain-specific error.


```fsharp
type Check<'value>
```


## Remarks

Use the `Check` module helpers to create and compose checks.


## Information

- **Module**: Global
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L450)

