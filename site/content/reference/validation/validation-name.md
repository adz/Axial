---
title: name
type: docs
---

Prefixes a validation with a named branch.


```fsharp
let name (name: string) (validation: Validation<'value, 'error>) : Validation<'value, 'error>
```




## Parameters

- `name`: The branch name.
- `validation`: The validation to scope.

## Returns

A validation whose diagnostics are prefixed with `Name name`.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L422)

