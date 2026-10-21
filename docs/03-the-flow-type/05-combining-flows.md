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

Prefer `flow {}` for a longer dependent sequence and pipelines for a short transformation. Both create the same Flow.

## Choosing where parallel branches start

`Flow.zipPar` and `Flow.sequencePar` start each branch inline, as `Task.WhenAll` does when given calls to async
methods. A branch runs on the current thread until it reaches an incomplete await. This keeps I/O and
already-completed flows cheap, but a synchronous CPU-bound branch can finish before the next branch starts.

Wrap only the CPU-bound branches that should start on the thread pool:

```fsharp transcript
> let cpuBranch value : Flow<string, int> = Flow.delay (fun () -> Flow.ok (value * value)) |> Flow.onThreadPool;;
> [ 1; 2; 3 ] |> List.map cpuBranch |> Flow.sequencePar |> Flow.run ();;
val it: Exit<int list,string> = Success [1; 4; 9]
```

`Flow.onThreadPool source` is `Flow.runOn RunOn.ThreadPool source`. `RunOn.Inline` leaves the flow unchanged.
On .NET, `RunOn.Scheduler scheduler` starts the flow on a particular `TaskScheduler` and keeps continuations after
awaits on that scheduler. `RunOn.DedicatedThread` gives a long blocking synchronous section a dedicated thread, but
only until its first incomplete await; use `Flow.fromBlocking` instead when a blocking foreign call must run to
completion after interruption.

`runOn` does not turn interruption into preemption. The branch receives the same cancellation token and observes it
at its normal checkpoints, such as `Flow.ensureNotCanceled`; parallel combinators still wait for branch cleanup.
If the token is already cancelled, the branch is not scheduled. JavaScript has one thread, so every `RunOn` case
runs inline when compiled with Fable.

## Related guides

- [Composition reference](/api/) lists mapping, binding, recovery,
  traversal, and sequential combination functions.
- [Fibers](/concurrency-and-state/fibers.html) introduces explicit child workflows.
- [Schedules](/scheduling-and-retries/index.html) adds retry and repetition policies without changing the
  underlying workflow.
