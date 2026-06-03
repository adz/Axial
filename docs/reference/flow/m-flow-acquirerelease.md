---
title: "Flow.acquireRelease"
linkTitle: "acquireRelease"
weight: 2403
---

Acquires a resource and registers its release with the current runtime scope.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.acquireRelease&#32;<span>acquire&#32;release</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `acquire` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'resource</span>&gt;</span></code> | The flow that acquires the resource. |
| `release` | <code><span>'resource&#32;->&#32;<a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a>&#32;->&#32;<a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task">Task</a></span></code> | The release action to run when the current scope closes. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'resource</span>&gt;</span></code> | A flow that succeeds with the acquired resource. |

## Remarks


 The resource is not released when this expression finishes. It is released when the
 surrounding runtime scope closes, which makes it suitable for resources acquired by
 subflows and then shared by later work in the same execution boundary.
