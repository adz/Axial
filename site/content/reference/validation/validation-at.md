---
title: at
type: docs
---

Scopes a validation under the supplied path segments.


```fsharp
let at (path: PathSegment list) (validation: Validation<'value, 'error>) : Validation<'value, 'error>
```




## Parameters

- `path`: The path segments to apply to the validation.
- `validation`: The validation to scope.

## Returns

A validation nested under the given path.

## Information

- **Module**: `Validation`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Validate.fs#L392)

