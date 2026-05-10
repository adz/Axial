---
title: "FsFlow.Needs"
linkTitle: "Needs"
type: docs
---

Describes the capability contract for a single dependency.

## Remarks

Named cap-set interfaces inherit this contract once and then expose the dependency through a
 member such as `Clock` or `Logger`. Workflow builders can accept any environment
 that implements `Needs&lt;'dep&gt;`, which lets larger runtimes satisfy smaller
 boundaries.


## Examples

```fsharp
type IClock =
     abstract UtcNow : unit -&gt; DateTimeOffset

 type ClockCaps =
     inherit Needs&lt;IClock&gt;
     abstract Clock : IClock
```

