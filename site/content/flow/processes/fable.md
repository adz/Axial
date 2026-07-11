---
weight: 50
title: Fable And Node
description: Understand which stream and process capabilities are portable.
type: docs
---


This page shows the platform boundary between portable stream programs and concrete operating-system process launchers.

`FlowStream` lives in `Axial.Flow` and uses the existing `Platform.fs` execution abstraction. Its constructors,
transformations, typed failures, backpressure, and consumers are Fable-compatible; those semantics should not move to a
separate package.

The process model (`Command`, `Pipeline`, events, transcripts, and `IProcess`) is also service-oriented. A browser cannot
launch an operating-system child process, so `Process.live` is .NET-only. Browser applications can still compile code
against a supplied `IProcess` implementation when execution is delegated to a remote worker.

A direct Node implementation should live in an `Axial.Flow.Process.Node` adapter backed by Node's `child_process`
API. That package boundary is preferable to adding Node imports to the portable process contract or placing process
launching logic in `Axial.Flow.Platform.fs`. `Platform.fs` owns Flow executor mechanics; Node child processes are an
operational service implementation.

This split keeps three concerns clear:

- `Axial.Flow`: portable streams and effect composition.
- `Axial.Flow.Process`: portable command vocabulary and the process capability contract, with the .NET live service.
- `Axial.Flow.Process.Node`: Node-specific process launching and stream wiring when implemented.

Portable endpoints use text, bytes, files, `Async` callbacks, and Flow streams. The .NET build additionally exposes
`System.IO.Stream` and `TextWriter` adapters. A Node adapter should expose readable/writable and Web Stream adapters
without adding JavaScript types to the portable contract. Use platform-specific implementation files for small
dependency-free differences; use an adapter package when host stream types or runtime packages enter the public API.
