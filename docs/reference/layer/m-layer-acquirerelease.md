---
title: "Layer.acquireRelease"
linkTitle: "acquireRelease"
weight: 2206
---

Acquires a resource and registers its release with the layer scope.

## Signature

<div class="fsdocs-usage">
<code><span>Layer.acquireRelease&#32;<span>acquire&#32;release</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `acquire` | <code><span><a href="t-layer.md">Layer</a>&lt;<span>'input,&#32;'error,&#32;'resource</span>&gt;</span></code> | The layer that acquires the resource. |
| `release` | <code><span>'resource&#32;->&#32;<a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a>&#32;->&#32;<a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task">Task</a></span></code> | The release action to run when the layer scope closes. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-layer.md">Layer</a>&lt;<span>'input,&#32;'error,&#32;'resource</span>&gt;</span></code> | A layer that succeeds with the acquired resource. |

## Remarks


 Use this for service implementations or provisioned resources that must live for the
 full <code>Flow.provide</code> boundary rather than only for the construction expression.
