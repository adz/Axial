---
title: "Flow.toTask"
linkTitle: "toTask"
weight: 2204
---

Executes a flow and returns a task that resolves to the final exit outcome.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.toTask&#32;<span>environment&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `environment` | <code>'env</code> | The environment required by the flow. |
| `flow` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The workflow to execute. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1">Task</a>&lt;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-exit-2.html">Exit</a>&lt;<span>'value,&#32;'error</span>&gt;</span>&gt;</span></code> | A task that completes with the fiber&#39;s final outcome. |
