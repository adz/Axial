---
title: "Flow.Flow.ToTask"
linkTitle: "ToTask"
weight: 2201
---

Starts the workflow and returns a task handle that completes with the final exit.

**Platform:** .NET only

## Signature

<div class="fsdocs-usage">
<code><span>this.ToTask</span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `environment` | <code>'env</code> | The environment used by the workflow. |
| `?cancellationToken` | <code><a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a></code> | The optional cancellation token. Defaults to <a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken.none">CancellationToken.None</a>. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1">Task</a>&lt;<span><a href="../../exit/t-flow-exit.md">Exit</a>&lt;<span>'value,&#32;'error</span>&gt;</span>&gt;</span></code> | A task that completes with the workflow exit. |
