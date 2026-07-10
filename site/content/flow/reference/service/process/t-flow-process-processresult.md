---
title: "Flow.Process.ProcessResult"
linkTitle: "ProcessResult"
weight: 1001
type: docs
---

Represents the captured outcome of an external process pipeline.

## Signature

<div class="fsdocs-usage">
<code>type ProcessResult</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `ExitCode` | The exit code returned by the last process. |
| `StdOut` | The standard output produced by the last process. |
| `StdErr` | The combined standard error produced by every process. |
| `ExitCodes` | The exit code of each process, from left to right. |
