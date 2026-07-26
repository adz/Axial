---
weight: 1
title: Axial.Flow
description: The Flow type, getting started, dependencies, concurrency, and the full guide list.
---

# Axial.Flow

## Core Flow

- [Getting Started](/flow/getting-started/) — learn the Flow type, creation, execution, composition, failures,
  dependencies, and application lifecycle one step at a time.
- [Application Lifecycle](../applications/) — root applications with `App.run`, `App.start`, and coordinated stop.
- [Task and Async Interop](../core-concepts/task-async-interop/) — binding `Async`, `Task`, `ValueTask`, and attempt constructors.
- [Defects and Exceptions](../core-concepts/defects/) — typed failures, defects, interruption, and exception recovery.
- [Bind](../core-concepts/bind/) — bind-site error assignment and mapping inside `flow {}`.
- [Flow Tutorials](../tutorials/) — service, runtime, environment, and layer walkthroughs.

## Dependencies and Resources

- [Dependencies](../services-and-runtimes/dependencies/) — records, services, layers, scopes, and host boundaries.
- [Explicit Services](../services-and-runtimes/explicit-services/) — reusable service contracts with `IHas<'service>`.
- [Layers](../services-and-runtimes/layers/) — building explicit environments.
- [Scopes and Resources](../services-and-runtimes/scopes-and-resources/) — resource lifetime and cleanup.
- [Building a Base Runtime](../services-and-runtimes/building-a-base-runtime/) — standard operational services.
- [Service Provider Boundaries](../services-and-runtimes/service-provider-boundaries/) — deliberate `IServiceProvider` edges.
- [Hosting](../hosting/) — standalone .NET, Generic Host, Node, and browser application edges.
- [Packages and Platforms](../packages-and-platforms/) — package boundaries and .NET, Node, and browser support.

## State and Concurrency

- [Fibers](../concurrency/fibers/) — background workflow execution.
- [Deferred and Semaphore](../concurrency/deferred-semaphore/) — coordination primitives.
- [Ref](../concurrency/ref/) — atomic mutable references.
- [Schedule](../concurrency/schedule/) — retry and repeat policies.
- [STM](../concurrency/stm/) — transactional memory.
- [Stream](../concurrency/stream/) — effectful pull-based streams.

## In Practice

- [Runnable Examples](../examples/) — executed during the docs build, mirrored back into the site.
- [Troubleshooting Types](../core-concepts/troubleshooting-types/) — the compiler errors that mean a wrapper boundary was crossed.
- Comparisons: [vs Effect-TS](../comparisons/effect-ts-comparison/), [FSharpPlus integration](../comparisons/fsharpplus-comparison/).
