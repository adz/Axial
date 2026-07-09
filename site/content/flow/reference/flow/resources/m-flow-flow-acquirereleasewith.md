---
title: "Flow.acquireReleaseWith"
linkTitle: "acquireReleaseWith"
weight: 2504
type: docs
---

Acquires a resource, uses it, and always runs the release action.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.acquireReleaseWith&#32;<span>acquire&#32;release&#32;useResource</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `acquire` | <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'resource</span>&gt;</span></code> | The flow that acquires the resource. |
| `release` | <code><span>'resource&#32;->&#32;<a href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken">CancellationToken</a>&#32;->&#32;<a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task">Task</a></span></code> | The release action to run after the resource is used. |
| `useResource` | <code><span>'resource&#32;->&#32;<span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></span></code> | The flow that uses the acquired resource. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that releases the resource after use, including failure paths. |

## Remarks


 Use this for lexical acquire/use/release. For resources that should live until the
 surrounding scope closes, use <a href="../t-flow-flow.md#acquireRelease">Flow.acquireRelease</a>.
