---
title: "Flow.Flow.RunSynchronously"
linkTitle: "RunSynchronously"
weight: 2203
---

Starts the workflow and blocks until the final exit is available.

**Platform:** .NET only

## Signature

<div class="fsdocs-usage">
<code><span>this.RunSynchronously</span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `environment` | <code>'env</code> | The environment used by the workflow. |
| `?timeout` | <code>int</code> | The optional timeout in milliseconds. |
| `?cancellationToken` | <code><a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a></code> | The optional cancellation token. Defaults to <a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken.none">CancellationToken.None</a>. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../../exit/t-flow-exit.md">Exit</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The final workflow exit. |
