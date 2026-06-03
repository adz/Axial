---
weight: 50
title: State and Concurrency
description: Shared state, coordination, and streaming in FsFlow.
---

# State and Concurrency

FsFlow provides several primitives for managing shared state and coordinating concurrent workflows. These tools allow you to build complex, highly-available systems while maintaining the benefits of a Result-based model.

## Overview

### [Ref (Atomic References)](./ref/)
`Ref<'T>` provides a thread-safe handle for mutable state. It is perfect for shared counters, flags, or small pieces of state that need to be updated atomically across multiple [**fibers**]({{< relref "/docs/core-model/fibers.md" >}}).

### [Deferred and Semaphore](./deferred-semaphore/)
`Deferred<'error, 'value>` coordinates one-shot typed results between fibers, while `FlowSemaphore` limits concurrent workflow sections with scoped permit release.

### [Schedule (Retries & Repetition)](./schedule/)
The `Schedule` module provides a powerful language for describing how and when a workflow should be retried upon failure or repeated upon success.

### [STM (Software Transactional Memory)](./stm/)
STM allows you to compose multiple atomic operations on `TRef` (transactional references) into a single transaction. It provides serializable consistency for in-memory state.

### [Stream (FlowStream)](./stream/)
FlowStream is an effectful, pull-based stream. It allows you to process large amounts of data asynchronously while respecting the FsFlow environment and error model.
