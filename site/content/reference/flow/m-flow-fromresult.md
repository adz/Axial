---
title: "FsFlow.Flow.fromResult"
linkTitle: "fromResult`"
type: docs
---

Lifts a `Result` into a synchronous flow.



## Examples

```fsharp
let res = Ok "success"
 let flow = Flow.fromResult res
```

