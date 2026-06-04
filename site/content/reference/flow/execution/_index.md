---
title: "Execution"
type: docs
---

This page shows the execution members that turn a cold flow description into a running handle or a blocking exit.

- [`Flow.ToAsync`](./m-flow-toasync.md): Starts the workflow and returns an F# async handle that completes with the final exit.
- [`Flow.ToTask`](./m-flow-totask.md): Starts the workflow and returns a task handle that completes with the final exit.
- [`Flow.ToValueTask`](./m-flow-tovaluetask.md): Starts the workflow and returns a value-task handle that completes with the final exit.
- [`Flow.RunSynchronously`](./m-flow-runsynchronously.md): Starts the workflow and blocks until the final exit is available.
