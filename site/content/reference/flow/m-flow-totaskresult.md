---
title: "Flow.toTaskResult"
linkTitle: "toTaskResult"
weight: 2205
type: docs
---

Executes a flow and returns a task that resolves to a standard result.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.toTaskResult&#32;<span>environment&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `environment` | <code>'env</code> | The environment required by the flow. |
| `flow` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The workflow to execute. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1">Task</a>&lt;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span>&gt;</span></code> | A task that completes with a <a href="https://learn.microsoft.com/dotnet/api/system.result-2">Result</a> representing the successful value or domain failure. |

## Remarks

Interruption signals and defects are raised as exceptions in the caller&#39;s context.
