---
title: "Services Process"
weight: 50
type: docs
---

This page shows the external-process service package. `IProcess` models command execution as an asynchronous workflow service and returns a `ProcessResult` with exit code, standard output, and standard error. Keep process execution behind this service contract so tests can return deterministic results without shelling out.

## Service

- [`Flow.Process.IProcess`](./t-flow-process-iprocess.md): Provides asynchronous execution for typed external-process pipelines.
- [`Flow.Process.ProcessResult`](./t-flow-process-processresult.md): Represents the captured outcome of an external process pipeline.

## Helpers

- [`Flow.Process.execute`](./m-flow-process-process-execute.md): Executes one command. Prefer <code>command</code>, <code>|&gt;&gt;</code>, and <code>run</code> for new code.
- [`Flow.Process.live`](./p-flow-process-process-live.md): Creates a live process service backed by <a href="https://learn.microsoft.com/dotnet/api/system.diagnostics.process">Process</a>.
- [`Flow.Process.layer`](./p-flow-process-process-layer.md): Builds the live process service as a layer.
