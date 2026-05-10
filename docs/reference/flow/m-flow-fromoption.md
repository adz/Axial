---
title: "FsFlow.Flow.fromOption"
linkTitle: "fromOption`"
---

Lifts an option into a synchronous flow with the supplied error.



## Examples

```fsharp
let opt = Some "value"
 let flow = Flow.fromOption "missing" opt
```

