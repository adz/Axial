---
title: "Capabilities Process"
weight: 50
type: docs
---

This page shows the external-process capability package. `IProcess` models command execution as an asynchronous workflow dependency and returns a `ProcessResult` with exit code, standard output, and standard error. Use it for tooling, build automation, and integration boundaries where spawning a process is part of the application behavior. Keep process execution behind this interface so tests can return deterministic results without shelling out.

## Capability

- [`Capabilities.Process.IProcess`](./t-capabilities-process-iprocess.md): Provides asynchronous access to external process execution.
- [`Capabilities.Process.ProcessResult`](./t-capabilities-process-processresult.md): Represents the outcome of an external process execution.

## Helpers

- [`Capabilities.Process.Process.execute`](./m-capabilities-process-process-execute.md): Executes a process using the process environment and returns the result.
- [`Capabilities.Process.Process.live`](./m-capabilities-process-process-live.md): Creates a live process runner backed by <a href="https://learn.microsoft.com/dotnet/api/system.diagnostics.process">Process</a>.

