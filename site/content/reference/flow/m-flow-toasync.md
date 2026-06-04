---
title: "Flow.ToAsync"
linkTitle: "ToAsync"
weight: 2200
type: docs
---

Starts the workflow and returns an F# async handle that completes with the final exit.

**Platform:** Fable compatible

## Signature

<div class="fsdocs-usage">
<code><span>this.ToAsync</span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `environment` | <code>'env</code> | The environment used by the workflow. |
| `?cancellationToken` | <code><a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a></code> | The optional cancellation token to use instead of <code>Async.CancellationToken</code>. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpasync-1">Async</a>&lt;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-exit-2.html">Exit</a>&lt;<span>'value,&#32;'error</span>&gt;</span>&gt;</span></code> | An async handle that completes with the workflow exit. |
