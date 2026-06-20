---
title: "Flow.Flow.ToValueTask"
linkTitle: "ToValueTask"
weight: 2202
---

Starts the workflow and returns a value-task handle that completes with the final exit.

**Platform:** .NET only

## Signature

<div class="fsdocs-usage">
<code><span>this.ToValueTask</span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `environment` | <code>'env</code> | The environment used by the workflow. |
| `?cancellationToken` | <code><a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a></code> | The optional cancellation token. Defaults to <a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken.none">CancellationToken.None</a>. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1">ValueTask</a>&lt;<span><a href="/reference/Axial/axial-flow-exit-2.html">Exit</a>&lt;<span>'value,&#32;'error</span>&gt;</span>&gt;</span></code> | A value task that completes with the workflow exit. |
