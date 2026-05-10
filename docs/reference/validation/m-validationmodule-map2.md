---
title: "FsFlow.ValidationModule.map2"
linkTitle: "map2`"
---

Combines two validations, accumulating errors if both fail.

## Remarks

This is the core applicative operation. If both `left` and 
 `right` fail, their diagnostics graphs are merged.


## Parameters

- `mapper`: A function of type `'left -> 'right -> 'value`.
- `left`: The first validation.
- `right`: The second validation.

## Returns

A validation with the combined result.

