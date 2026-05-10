---
title: "FsFlow.ValidationModule.map3"
linkTitle: "map3`"
type: docs
---

Combines three validations, accumulating errors when any input fails.



## Parameters

- `mapper`: A function of type `'left -> 'middle -> 'right -> 'value`.
- `left`: The first validation.
- `middle`: The second validation.
- `right`: The third validation.

## Returns

A validation with the combined result.

