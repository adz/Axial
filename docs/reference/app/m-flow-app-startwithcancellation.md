---
title: "Flow.App.startWithCancellation"
linkTitle: "startWithCancellation"
weight: 2101
---

Starts a root workflow linked to an external cancellation token.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.App.startWithCancellation&#32;<span>cancellationToken&#32;environment&#32;application</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | <code><a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a></code> | A host-owned token that requests application stop when cancelled. |
| `environment` | <code>'env</code> | The explicit environment supplied to the root workflow. |
| `application` | <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The root workflow to start. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-flow-apphandle.md">AppHandle</a>&lt;<span>'error,&#32;'value</span>&gt;</span></code> | A handle for observing completion or requesting coordinated stop. |
