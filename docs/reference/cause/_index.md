---
title: "Cause"
weight: 40
---

This page shows the `Cause<'error>` type, which distinguishes expected domain failures, unexpected technical defects, administrative interruptions, sequential failure composition, parallel failure composition, and diagnostic traces. Understanding the cause tree lets Axial preserve what happened during retries, cleanup, parallel execution, and observability boundaries without flattening everything into one exception or one typed error.

## Core type

- [`Flow.Cause`](./t-flow-cause.md):

## Module functions

- [`Flow.Cause.map`](./m-flow-cause-map.md): Transforms the error value of a failure cause using the provided function.
- [`Flow.Cause.thenCause`](./m-flow-cause-thencause.md): Combines causes that happened sequentially.
- [`Flow.Cause.both`](./m-flow-cause-both.md): Combines causes that happened concurrently.
- [`Flow.Cause.traced`](./m-flow-cause-traced.md): Attaches diagnostic trace text to a cause.
- [`Flow.Cause.failures`](./m-flow-cause-failures.md): Returns every typed failure value contained in a cause tree.
- [`Flow.Cause.defects`](./m-flow-cause-defects.md): Returns every defect exception contained in a cause tree.
- [`Flow.Cause.isInterrupted`](./m-flow-cause-isinterrupted.md): Returns whether the cause tree contains an interruption signal.
- [`Flow.Cause.prettyPrint`](./m-flow-cause-prettyprint.md): Pretty prints a cause tree for diagnostics.
