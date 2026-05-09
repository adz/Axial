---
title: Validation
linkTitle: Validation
---

An accumulating validation result that keeps the structured diagnostics graph visible.


```fsharp
type Validation<'value, 'error>
```


## Remarks

Unlike `FSharpResult`, this type is designed for applicative
composition using `and!` in the `validate { }` builder, which merges errors instead of
short-circuiting.


## Information

- **Module**: Global
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L56)

