---
title: "guessed-wait (AXG013): wait for facts, not time"
linkTitle: guessed-wait
description: Reports tests that sleep and hope background work has finished, instead of waiting for it.
---

# guessed-wait (AXG013)

## The problem

A test starts background work, sleeps 10 ms, then checks the result. On your machine 10 ms is enough. On a loaded CI
runner it is not, and the test fails one run in fifty. Raising it to 100 ms makes the suite slower and still does not
prove the work finished.

```fsharp
open System.Threading.Tasks

/// Reported: guesses that 10 ms is long enough.
let guessedWait (assertReady: unit -> unit) = task {
    do! Task.Delay 10
    assertReady ()
}
```

Wait for the fact instead. The worker completes a signal at the point the test needs; the timeout only decides when
to give up:

```fsharp
open System
open System.Threading.Tasks

/// Fixed: waits until the worker says it is ready.
let waitForReady (ready: TaskCompletionSource<unit>) (assertReady: unit -> unit) = task {
    let! completed = Task.WhenAny(ready.Task :> Task, Task.Delay(TimeSpan.FromSeconds 30.))
    if not (obj.ReferenceEquals(completed, ready.Task)) then
        raise (TimeoutException("Worker did not become ready"))
    do! ready.Task
    assertReady ()
}
```

A `Deferred` completed by the worker does the same in a flow. For code that waits on a schedule, step a test clock
instead of sleeping.

## What it reports

Only in `test`-role code:

- `Thread.Sleep`: an error. No note or allowance accepts it, even an infinite one.
- `Task.Delay` or `Async.Sleep` awaited directly by `do!` or `let!`: a guide warning. A sleep in a polling loop is
  reported too, because counting iterations is still a guessed time.

Allowed:

- A delay used as a deadline: `Task.WhenAny(work, Task.Delay timeout)`, `Wait(timeout)`, `WaitAsync(timeout)`,
  `SpinWait.SpinUntil(predicate, timeout)`.
- An infinite `Async.Sleep`, or an infinite `Task.Delay` that takes a cancellation token.
- `Flow.sleep`, `Clock.sleep` and test clock advances. Whether those wait in real time depends on the clock installed.

The rule checks direct calls, not delays hidden in helper functions.

## When it is deliberate

For an awaited delay that is really needed, such as generating load, add a note on the line or the line before:
`// axial-note guessed-wait: <reason>`. The finding stays visible as Info. Without a note it is an error at a
[checkpoint](../allowances-and-notes.html#checkpoints).
