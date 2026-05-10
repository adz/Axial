---
title: "FsFlow.Flow.succeed"
linkTitle: "succeed`"
---

Alias for `ok` that reads well in some call sites.



## Examples

```fsharp
let flow = Flow.succeed 42
 let result = Flow.run () flow
 // result = Success 42
```

