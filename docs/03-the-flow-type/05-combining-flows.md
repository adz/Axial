---
title: Combining Flows
description: Transform and combine Flow descriptions with ordinary F# pipelines.
---

# Combining Flows

`Flow.map` changes the successful value, `Flow.mapError` changes the expected error, and `Flow.bind` runs dependent
work (it is the function form of `let!`). `Flow.zip` runs two descriptions one after the other and keeps both values:

```fsharp transcript
> let loadUser (id: int) : Flow<string, string> = if id = 1 then Flow.ok "ada" else Flow.fail $"no user {id}";;
> loadUser 1 |> Flow.map (fun name -> name.ToUpperInvariant()) |> Flow.run ();;
val it: Exit<string,string> = Success "ADA"

> loadUser 2 |> Flow.mapError (fun message -> message.Length) |> Flow.run ();;
val it: Exit<string,int> = Failure (Fail 9)

> loadUser 1 |> Flow.bind (fun name -> Flow.ok $"Hello, {name}") |> Flow.run ();;
val it: Exit<string,string> = Success "Hello, ada"

> Flow.zip (loadUser 1) (Flow.ok 42) |> Flow.run ();;
val it: Exit<Tuple<string,int>,string> = Success ("ada", 42)
```

`Flow.map2` and `Flow.map3` combine the successful values directly. Concurrent composition is a separate choice;
use `Flow.zipPar` only when both branches are safe to run at the same time.

Prefer `flow {}` for a longer dependent sequence and pipelines for a short transformation. They create the same Flow
model and differ only in how the code reads.

## Go Further

- [Composition reference](/api/) lists mapping, binding, recovery,
  traversal, and sequential combination functions.
- [Fibers](/concurrency-and-state/fibers.html) introduces explicit child workflows.
- [Schedules](/scheduling-and-retries/index.html) adds retry and repetition policies without changing the
  underlying workflow.
