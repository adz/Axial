---
title: "Flow.runFull"
linkTitle: "runFull"
weight: 2201
type: docs
---

Executes a flow with an explicit cancellation token.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.runFull&#32;<span>environment&#32;cancellationToken&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `environment` | <code>'env</code> | The environment required by the flow. |
| `cancellationToken` | <code><a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a></code> | The token used to signal cancellation. |
| `flow` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The workflow to execute. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-effect-2.html">Effect</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | An effect that represents the asynchronous execution outcome. |

## Remarks

Uncaught exceptions become <code>Cause.Die</code>; cancellation becomes <code>Cause.Interrupt</code>.
