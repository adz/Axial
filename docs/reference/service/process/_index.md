---
title: "Services Process"
weight: 50
---

This page shows the external-process service package. `IProcess` models command execution as an asynchronous workflow service and returns a `ProcessResult` with exit code, standard output, and standard error. Keep process execution behind this service contract so tests can return deterministic results without shelling out.

## Service

- [`Flow.Process.IProcess`](./t-flow-process-iprocess.md): Provides asynchronous access to external process execution.
- [`Flow.Process.ProcessResult`](./t-flow-process-processresult.md): Represents the outcome of an external process execution.

## Helpers

- [`Flow.Process.execute`](./m-flow-process-process-execute.md): Executes a process through an explicit process service and returns the result.
- [`Flow.Process.live`](./p-flow-process-process-live.md): Creates a live process service backed by <a href="https://learn.microsoft.com/dotnet/api/system.diagnostics.process">Process</a>.
- [`Flow.Process.layer`](./p-flow-process-process-layer.md): Builds the live process service as a layer.
