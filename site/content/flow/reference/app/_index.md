---
title: "App"
weight: 500
---

This page shows the portable `App` lifecycle in `Axial.Flow`. Use `App.run` for a finite root workflow, or `App.start` when a console signal, host, window, UI owner, or test controls stop through an `AppHandle`. The handle keeps the final structured `Exit`, makes stop idempotent, and completes only after the root Flow scope has closed. Platform event subscription and error rendering stay in the host adapter.

## Lifecycle

- [`Flow.AppStatus`](./t-flow-appstatus.md): Describes the lifecycle state of a running application.
- [`Flow.AppHandle`](./t-flow-apphandle.md):
 Owns one running root workflow and provides coordinated application shutdown.

- [`Flow.AppHandle.Status`](./p-flow-apphandle-status.md): Gets the current application lifecycle state.
- [`Flow.AppHandle.Completion`](./p-flow-apphandle-completion.md): Waits for the root workflow and its scope finalizers to complete.
- [`Flow.AppHandle.Stop`](./m-flow-apphandle-stop.md): Requests cooperative interruption and waits for the final application exit.

## Start and run

- [`Flow.App.start`](./m-flow-app-start.md): Starts a root workflow and returns a handle that owns its lifetime.
- [`Flow.App.startWithCancellation`](./m-flow-app-startwithcancellation.md): Starts a root workflow linked to an external cancellation token.
- [`Flow.App.run`](./m-flow-app-run.md): Runs a root workflow to completion using the caller&#39;s asynchronous cancellation token.
- [`Flow.App.runWithCancellation`](./m-flow-app-runwithcancellation.md): Runs a root workflow to completion using an explicit host-owned cancellation token.
