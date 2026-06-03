---
title: "Cause"
weight: 40
type: docs
---

This page shows the `Cause<'error>` type, which distinguishes expected domain failures, unexpected technical defects, administrative interruptions, sequential failure composition, parallel failure composition, and diagnostic traces. Understanding the cause tree lets FsFlow preserve what happened during retries, cleanup, parallel execution, and observability boundaries without flattening everything into one exception or one typed error.

## Core type

- [`Cause`](./t-cause.md):
 Represents the cause of a failed workflow.


## Module functions

- [`Cause.map`](./m-cause-map.md): Transforms the error value of a failure cause using the provided function.
- [`Cause.thenCause`](./m-cause-thencause.md): Combines causes that happened sequentially.
- [`Cause.both`](./m-cause-both.md): Combines causes that happened concurrently.
- [`Cause.traced`](./m-cause-traced.md): Attaches diagnostic trace text to a cause.
- [`Cause.failures`](./m-cause-failures.md): Returns every typed failure value contained in a cause tree.
- [`Cause.defects`](./m-cause-defects.md): Returns every defect exception contained in a cause tree.
- [`Cause.isInterrupted`](./m-cause-isinterrupted.md): Returns whether the cause tree contains an interruption signal.
- [`Cause.prettyPrint`](./m-cause-prettyprint.md): Pretty prints a cause tree for diagnostics.
