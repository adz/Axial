---
title: run
---

Executes a synchronous flow with the provided environment.


```fsharp
let run (environment: 'env) (Flow operation: Flow<'env, 'error, 'value>) : Result<'value, 'error>
```




## Information

- **Module**: `Flow`
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L15)

## Examples

```fsharp
let flow = Flow.read (fun env -> $"Hello, {env}!")
let result = Flow.run "World" flow
// result = Ok "Hello, World!"
```

