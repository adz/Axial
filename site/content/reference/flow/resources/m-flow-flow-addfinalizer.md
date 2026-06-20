---
title: "Flow.Flow.addFinalizer"
linkTitle: "addFinalizer"
weight: 2400
type: docs
---

Registers an asynchronous finalizer with the current runtime scope.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.addFinalizer&#32;<span>finalizer</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `finalizer` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a>&#32;->&#32;<a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task">Task</a></span></code> | The finalizer to run when the current scope closes. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;unit</span>&gt;</span></code> | A flow that registers the finalizer. |

## Remarks


 Use this when a resource acquired by a subflow should live until the surrounding
 runtime or layer scope closes, rather than only until the current expression ends.
