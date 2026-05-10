---
title: "FsFlow.Env"
linkTitle: "Env"
type: docs
---

Request token for projecting a value from a dependency.

## Remarks

Builders read the dependency from the environment, apply the projection, and then reuse the
 existing lift/bind behavior for the projected value. If the projection returns a
 `Result`, `Async`, `Task`, `ValueTask`, `ColdTask`, `option`, or
 `voption`, the existing workflow rules still apply.


## Examples

```fsharp
type IClock =
     abstract UtcNow : unit -&gt; DateTimeOffset

 type ClockCaps =
     inherit Needs&lt;IClock&gt;
     abstract Clock : IClock

 let readClockNow : Flow&lt;#ClockCaps, unit, DateTimeOffset&gt; =
     flow {
         let! now = Env&lt;IClock&gt; _.UtcNow
         return now
     }
```

