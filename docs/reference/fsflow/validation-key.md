---
title: Validation.key
linkTitle: key
---

Prefixes a validation with a keyed branch.


```fsharp
let key (key: string) (validation: Validation<'value, 'error>) : Validation<'value, 'error>
```




## Parameters

- `key`: The branch key.
- `validation`: The validation to scope.

## Returns

A validation whose diagnostics are prefixed with `Key key`.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L408)

