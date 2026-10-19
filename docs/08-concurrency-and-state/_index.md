---
title: Concurrency and State
description: Fibers, atomic references, coordination primitives, scheduling, STM, and streams.
---

# Concurrency and State

Structured concurrency with fibers, shared state with Ref and STM, coordination with Deferred and Semaphore, retry and repeat with Schedule, and incremental data with FlowStream.

- [Fibers](fibers.html): structured child fibers, fork/join, and interruption.
- [Supervision](supervision.html): restart policies for failing workers.
- [Ref](ref.html): atomic single-variable updates.
- [Deferred and Semaphore](deferred-semaphore.html): coordination primitives.
- [STM](stm.html): multi-variable atomic transactions.
- [Queue](queue.html) and [Hub](hub.html): value handoff between fibers.
- [SubscriptionRef](subscription-ref.html): current state consumers can join late.
- [Cache and memoize](cache.html): single-flight lookups.
- [Scheduling and retries](/scheduling-and-retries/): `Schedule` with `Flow.retry` and `Flow.repeat`.
- [Streams](/streams/): incremental `FlowStream` data.

The [torture tests](torture-tests/index.html) run every construct in this section under interruption, failure, and
shutdown, and check every guarantee it makes.
