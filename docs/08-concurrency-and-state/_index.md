---
title: Concurrency and State
description: Fibers, atomic references, coordination primitives, scheduling, STM, and streams.
---

# Concurrency and State

Structured concurrency with fibers, shared state with Ref and STM, coordination with Deferred and Semaphore, retry and repeat with Schedule, and incremental data with FlowStream.

The [torture tests](torture-tests/index.html) run every construct in this section under interruption, failure, and
shutdown, and check every guarantee it makes.
