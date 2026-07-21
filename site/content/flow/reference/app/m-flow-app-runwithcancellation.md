---
title: "Flow.App.runWithCancellation"
linkTitle: "runWithCancellation"
weight: 2103
type: docs
---

Runs a root workflow to completion using an explicit host-owned cancellation token.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.App.runWithCancellation&#32;<span>cancellationToken&#32;environment&#32;application</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | <code><a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a></code> | A host-owned token that requests application stop when cancelled. |
| `environment` | <code>'env</code> | The explicit environment supplied to the root workflow. |
| `application` | <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The root workflow to run. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpasync-1">Async</a>&lt;<span><a href="../exit/t-flow-exit.md">Exit</a>&lt;<span>'value,&#32;'error</span>&gt;</span>&gt;</span></code> | The final exit after the root scope has closed. |
