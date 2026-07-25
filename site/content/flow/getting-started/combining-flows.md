---
weight: 7
title: Combining Flows
description: Transform and combine Flow descriptions with ordinary F# pipelines.
type: docs
---


Use [`Flow.map`]({{< relref "/flow/reference/flow/composition/m-flow-flow-map.md" >}}) when only the successful value changes:

```fsharp
loadUser userId
|> Flow.map _.DisplayName
```

Use [`Flow.mapError`]({{< relref "/flow/reference/flow/composition/m-flow-flow-maperror.md" >}}) when the caller needs a different expected error type:

```fsharp
loadUser userId
|> Flow.mapError UserLoadFailed
```

Use [`Flow.bind`]({{< relref "/flow/reference/flow/composition/m-flow-flow-bind.md" >}}) for dependent work. It is the function form of `let!`:

```fsharp
loadUser userId
|> Flow.bind sendGreeting
```

Use [`Flow.zip`]({{< relref "/flow/reference/flow/composition/m-flow-flow-zip.md" >}}) when two descriptions should run sequentially and both values are needed:

```fsharp
Flow.zip loadProfile loadPreferences
// Flow<AppEnv, AppError, Profile * Preferences>
```

[`Flow.map2`]({{< relref "/flow/reference/flow/composition/m-flow-flow-map2.md" >}}) and [`Flow.map3`]({{< relref "/flow/reference/flow/composition/m-flow-flow-map3.md" >}}) combine the successful values directly. Concurrent composition is a separate choice;
use [`Flow.zipPar`]({{< relref "/flow/reference/flow/concurrency/m-flow-flow-zippar.md" >}}) only when both branches are safe to run at the same time.

Prefer `flow {}` for a longer dependent sequence and pipelines for a short transformation. They create the same Flow
model and differ only in how the code reads.

## Go Further

- [Composition reference]({{< relref "/flow/reference/flow/composition/" >}}) lists mapping, binding, recovery,
  traversal, and sequential combination functions.
- [Fibers]({{< relref "/flow/concurrency/fibers/" >}}) introduces explicit child workflows.
- [Schedules]({{< relref "/flow/concurrency/schedule/" >}}) adds retry and repetition policies without changing the
  underlying workflow.
