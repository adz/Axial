---
title: "FsFlow.Flow.fail"
linkTitle: "fail`"
---

Alias for `error` that reads well in some call sites.



## Examples

```fsharp
let flow = Flow.fail "error"
 let result = Flow.run () flow
 // result = Failure (Cause.Fail "error")
```

