---
title: "FsFlow.Flow.run"
linkTitle: "run`"
type: docs
---

Executes a flow with the provided environment and the default cancellation token.



## Examples

```fsharp
let flow = Flow.read (fun env -> $"Hello, {env}!")
 let result = Flow.run "World" flow
 // result = Promise that resolves to Success "Hello, World!" on Fable, or Success "Hello, World!" on .NET
```

