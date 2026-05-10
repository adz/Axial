---
title: "FsFlow.Builders.flow"
linkTitle: "flow"
type: docs
---

The universal `flow { }` computation expression.

## Remarks

<para>
 Use this builder when the boundary can mix synchronous values, `Async`, `Task`,
 `Result`, and environment requests while keeping typed failures and explicit
 dependency access.
 </para>
 <para>
 It preserves the current environment model while allowing the workflow to compose
 task-oriented inputs directly, so callers do not need to switch builders just to cross
 an async boundary.
 </para>


## Examples

```fsharp
 let greeting =
     flow {
         let! name = Flow.env
         let! suffix = async { return "!" }
         return $"Hello, {name}{suffix}"
     }
 ```

