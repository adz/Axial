---
title: Torture Tests
description: "Sixteen runnable scenarios that drive every concurrency construct under interruption, failure, and shutdown, and check the guarantees each one makes."
---

# Torture Tests

Most of Axial's concurrency guarantees are about races. An interrupted take never loses a value. A lossless
subscriber sees every value in order. A scope releases what it acquired, however its fibers end. Each guarantee is easy
to state and easy to break by accident, and a race that breaks it only now and then will not show up in a small example.

Each torture scenario runs one area of the library hard: many fibers, random interruptions, failures, and shutdowns in
the middle of the work. Then it checks the invariants that must hold whatever the interleaving was. A check never
depends on timing, such as "this finished within 10 ms", so a violated check is a defect, not a slow machine.

## Run them yourself

The scenarios are a runnable example in
[`examples/Axial.TortureTest`](https://github.com/adz/Axial/tree/main/examples/Axial.TortureTest). From a clone of the
repository, run every scenario for 10 rounds:

```bash
dotnet run --project examples/Axial.TortureTest
```

Name a scenario, a round count, or both:

```bash
dotnet run --project examples/Axial.TortureTest -- queues 200
dotnet run --project examples/Axial.TortureTest -- all 100
```

The program prints each invariant with a check mark and the number of rounds it held on. It exits with a non-zero code
if any invariant was ever violated, so it can gate a CI job or run as a soak test on your own hardware.

## Rounds and seeds

A scenario runs once per round, and each round has a seed. The seed fixes every random choice the scenario makes: which
fiber to interrupt, which strategy a subscriber uses, how long an operation waits. A violated invariant is reported
with the seeds it failed on, so the same choices can be run again. The interleaving of fibers still differs from run to
run, which is what the rounds are for.

The runner around the scenarios:

{{< snippet id="torture-runner" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

## How Axial runs them

- On every build, the .NET test suite runs every scenario for 10 seeds and fails if any invariant is violated.
- The same scenario files are compiled to JavaScript with Fable and run on Node at a fifth of their size, so the
  JavaScript runtime keeps the same guarantees.
- A coverage test fails when a public member of the runtime and concurrency modules is not used by any scenario. A new
  API in those modules ships with a scenario that exercises it.

The scenarios have found real defects: a sleep that could end early, failures that recovery combinators did not see
once they had been traced, a value lost when a stream's timer fired at the moment it arrived, and, on JavaScript,
fibers that lost their scope after a long synchronous stretch.

## Scenarios

| Scenario | What it stresses |
| --- | --- |
| [Pipeline](pipeline.html) | Queues, a hub, a `SubscriptionRef`, and graceful fibers together, stopped by closing a scope. |
| [Queues](queues.html) | Producers and consumers on one queue, with takes and offers interrupted, and every strategy. |
| [Hubs](hubs.html) | Subscribers joining and leaving throughout, and every publish style. |
| [Semaphores](semaphores.html) | Permits under contention, with waiters and holders interrupted. |
| [Deferreds](deferreds.html) | Completions racing each other, and awaiters interrupted or arriving late. |
| [Refs](refs.html) | `Ref` and `SubscriptionRef` updated concurrently. |
| [STM](stm.html) | Transfers, blocking withdrawals, and atomic audits. |
| [Fibers](fibers.html) | Latest-wins slots, inherited context, the fiber registry, and observers. |
| [Caches](caches.html) | Single-flight lookups with interrupted callers, failures, and invalidation. |
| [Schedules](schedules.html) | Retry, repeat, supervise, and timeouts. |
| [Parallel](parallel.html) | Bounded traversals, pooled resources, fail-fast zips, and races. |
| [Errors](errors.html) | Every outcome from concurrent branches, recovered, combined, and converted. |
| [Scopes](scopes.html) | Nested scopes and forked children with every kind of resource. |
| [Layers](layers.html) | Resources provisioned in sequence and in parallel, with failures and interruption. |
| [Streams](streams.html) | Every stream operator, against the List model or the property it promises. |
| [Interop](interop.html) | Task, ValueTask, Async, and blocking calls, interrupted at random. |
