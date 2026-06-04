---
title: "Concurrency"
type: docs
---

This page shows the Flow helpers that fork work, coordinate fibers, and run independent workflows in parallel.

- [`Flow.fork`](./m-flow-fork.md): Starts a flow in a new fiber without waiting for it to complete.
- [`Flow.join`](./m-flow-join.md): Waits for a fiber to complete and returns its successful value or typed failure.
- [`Flow.interrupt`](./m-flow-interrupt.md): Signals a fiber to stop and waits for it to finish its cleanup.
- [`Flow.zipPar`](./m-flow-zippar.md): Combines two flows into a tuple of their values, running them concurrently.
- [`Flow.race`](./m-flow-race.md): Runs two flows concurrently and returns the result of the first one to complete.
