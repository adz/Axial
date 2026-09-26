---
title: Concurrency and State
description: Fibers, atomic references, coordination primitives, scheduling, STM, and streams.
---

# Concurrency and State

Structured concurrency with fibers, shared state with Ref and STM, coordination with Deferred and Semaphore, retry and repeat with Schedule, and incremental data with FlowStream.

Queues, hubs, and `SubscriptionRef` hand values between fibers. The [torture test](torture-test.html) runs them together
under interruption and shutdown and checks every guarantee they make.
