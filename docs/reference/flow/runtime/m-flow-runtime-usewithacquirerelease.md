---
title: "Flow.Runtime.useWithAcquireRelease"
linkTitle: "useWithAcquireRelease"
---

Acquires a resource, uses it, and always runs the release action.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Runtime.useWithAcquireRelease&#32;<span>acquire&#32;release&#32;useResource</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `acquire` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'resource</span>&gt;</span></code> | The flow that acquires the resource. |
| `release` | <code><span>'resource&#32;->&#32;<a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a>&#32;->&#32;<a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task">Task</a></span></code> | The task-based release action. |
| `useResource` | <code><span>'resource&#32;->&#32;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></span></code> | The flow that uses the acquired resource. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that releases the resource after use, including failure paths. |

