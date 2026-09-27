---
title: Interop
description: "Task, ValueTask, Async, blocking calls, and cold tasks brought into flows, with a third of the calls interrupted."
---

# Interop

This scenario brings foreign [asynchronous operations](/the-flow-type/task-async-interop.html) into flows, interrupts
a third of them, and checks that every outcome maps to a flow outcome.

Run it with `dotnet run --project examples/Axial.TortureTest -- interop 100`.

## What it does

- A hundred and twenty calls each adapt one kind of operation: `Flow.fromAsync`, `fromAsyncResult`, `attemptAsync`,
  `fromBlocking`, `fromBlockingResult`, `attemptBlocking`, and on .NET the `Task` and `ValueTask` adapters, including
  those for tasks that have already started. Each operation succeeds, returns an error, or throws, at random.
- One flow runs directly, through `Flow.toAsync`, and on .NET through `Flow.startTask` and `Flow.run`.
- On .NET, `ColdTask` values are created, then bound in `flow { }`.
- A flow that never suspends reads `Flow.cancellationToken`, loops with `Flow.ensureNotCanceled`, and is interrupted.

On JavaScript, the Task adapters and cold tasks do not exist, and a blocking call only checks its token.

{{< snippet id="torture-interop" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

## What each check proves

| Check | Guarantee |
| --- | --- |
| Every adapted call produced a value, a typed failure, a defect, or an interruption | A thrown exception becomes a defect, an `Error` becomes a typed failure, and a cancelled token becomes an interruption. |
| A flow run as a task or an async has the same exit | Every way of running a flow reports the same outcome. |
| Cold tasks started only when bound, once per binding | A `ColdTask` does no work until a flow binds it. |
| A flow checking its token stopped when interrupted, and the token it read was cancelled | Interruption cancels the token that `Flow.cancellationToken` returns, and `Flow.ensureNotCanceled` observes it. |
