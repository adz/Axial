---
title: map3
type: docs
---

Combines three task flows with a mapping function.


```fsharp
let map3 (mapper: 'left -> 'middle -> 'right -> 'value) (left: TaskFlow<'env, 'error, 'left>) (middle: TaskFlow<'env, 'error, 'middle>) (right: TaskFlow<'env, 'error, 'right>) : TaskFlow<'env, 'error, 'value>
```




## Information

- **Module**: `TaskFlow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L487)

