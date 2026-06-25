---
weight: 50
title: State and Concurrency
description: Shared state, coordination, and streaming in Axial.
type: docs
---


Axial provides primitives for shared state and concurrent workflows.

## Overview

### [Ref (Atomic References)](./ref/)
`Ref<'T>` provides a thread-safe handle for mutable state such as counters, flags, or small values shared across [**fibers**]({{< relref "/docs/core-model/fibers.md" >}}).

### [Deferred and Semaphore](./deferred-semaphore/)
`Deferred<'error, 'value>` coordinates one-shot typed results between fibers, while `FlowSemaphore` limits concurrent workflow sections with scoped permit release.

### [Schedule (Retries & Repetition)](./schedule/)
The `Schedule` module describes how and when a workflow should be retried after failure or repeated after success.

### [STM (Software Transactional Memory)](./stm/)
STM allows you to compose multiple atomic operations on `TRef` (transactional references) into a single transaction. It provides serializable consistency for in-memory state.

### [Stream (FlowStream)](./stream/)
FlowStream is an effectful, pull-based stream that keeps the Axial environment and error model.
