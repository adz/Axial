---
weight: 7
title: Combining Flows
description: Transform and combine Flow descriptions with ordinary F# pipelines.
type: docs
---


Use `Flow.map` when only the successful value changes:

```fsharp
loadUser userId
|> Flow.map _.DisplayName
```

Use `Flow.mapError` when the caller needs a different expected error type:

```fsharp
loadUser userId
|> Flow.mapError UserLoadFailed
```

Use `Flow.bind` for dependent work. It is the function form of `let!`:

```fsharp
loadUser userId
|> Flow.bind sendGreeting
```

Use `Flow.zip` when two descriptions should run sequentially and both values are needed:

```fsharp
Flow.zip loadProfile loadPreferences
// Flow<AppEnv, AppError, Profile * Preferences>
```

`Flow.map2` and `Flow.map3` combine the successful values directly. Concurrent composition is a separate choice;
use `Flow.zipPar` only when both branches are safe to run at the same time.

Prefer `flow {}` for a longer dependent sequence and pipelines for a short transformation. They create the same Flow
model and differ only in how the code reads.
