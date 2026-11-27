---
title: "raw-concurrency (AXG009): let Flow own concurrent work"
linkTitle: raw-concurrency
description: Reports Task.Run, TaskCompletionSource, threads and other raw concurrency outside files that claim it.
---

# raw-concurrency (AXG009)

## The problem

Work started with `Task.Run` belongs to nobody. When the flow that started it is interrupted, the task keeps going.
Nothing waits for its cleanup, and if it crashes, nobody hears. A forked fiber is owned by its scope: interrupting the
parent interrupts it, the scope waits for it to finish, and its defects are reported.

```fsharp
/// Reported: nothing interrupts, awaits or reports this work.
let refreshInBackground (refresh: unit -> unit) =
    System.Threading.Tasks.Task.Run(fun () -> refresh ())

/// Fixed: the fiber belongs to the caller's scope.
let refreshOwned (refresh: Flow<unit, Never, unit>) =
    flow {
        let! fiber = Flow.fork refresh
        return fiber
    }
```

Use `Flow.fork`, `Flow.race`, `Flow.zipPar`, `Queue`, `Deferred` and `Semaphore` instead of raw tasks and threads.

## What it reports

Constructing a `Task`, `TaskCompletionSource`, `CancellationTokenSource` or `Thread`; `Task.Run`,
`Task.Factory.StartNew`, `ContinueWith`; and `ThreadPool` calls.

`lock`, `Monitor.Enter`/`Exit`, `Interlocked` and `Volatile` are not reported: they guard state and start nothing.
Blocking waits such as `SemaphoreSlim.Wait` or `Thread.Join` are reported by [`raw-effect`](raw-effect.html).

Test projects are exempt, unless `AxialGuardrailsInTests=all`.

## Where raw concurrency belongs

Some code has to use raw tasks, such as an adapter for an OS callback. Two ways to allow it:

- **A claim**, for a file whose job is raw concurrency:
  `<Compile Include="Bridge.fs" AxialCapability="concurrency" />`. `raw-concurrency`, `dropped-work` and
  `task-surface` skip that file.
- **An allowance**, for one call anywhere else:
  `// axial-allow-raw-concurrency: (why: the OS callback completes it on its own thread)`, or `(gap: name)` for a
  missing Axial feature.

A `host` project can also be listed under [`[exemptions] raw-concurrency`](../policy-file.html#exempt-a-host-project-from-a-concurrency-rule).
