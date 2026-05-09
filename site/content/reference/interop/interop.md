---
title: Interop
type: docs
---

This page shows the interop helpers that bridge task, async, and synchronous boundaries in FsFlow.

## TaskFlow bridges

- [`TaskFlow.fromFlow`](./taskflow-fromflow.md) [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L261)
- [`TaskFlow.fromAsyncFlow`](./taskflow-fromasyncflow.md) [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L264)
- [`TaskFlow.orElseTask`](./taskflow-orelsetask.md) [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L185)
- [`TaskFlow.orElseAsync`](./taskflow-orelseasync.md): Turns a pure validation result into a task flow with task-provided failure. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L200)
- [`TaskFlow.orElseFlow`](./taskflow-orelseflow.md) [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L213)
- [`TaskFlow.orElseAsyncFlow`](./taskflow-orelseasyncflow.md) [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L227)
- [`TaskFlow.orElseTaskFlow`](./taskflow-orelsetaskflow.md) [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L245)
- [`Flow.provideLayer`](./flow-providelayer.md): Provides a derived environment from a layer flow to a downstream flow. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Flow.fs#L331)
- [`AsyncFlow.provideLayer`](./asyncflow-providelayer.md): Provides a derived environment from a layer flow to a downstream flow. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/AsyncFlow.fs#L313)
- [`TaskFlow.provideLayer`](./taskflow-providelayer.md): Provides a derived environment from a layer flow to a downstream task flow. [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L569)

## Builder extensions

- module `TaskFlowBuilderExtensions` [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L1290)
- module `AsyncFlowBuilderExtensions`: [omit] [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/TaskFlow.fs#L1444)

