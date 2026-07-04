---
title: "Flow.Flow.attemptTask"
linkTitle: "attemptTask"
weight: 2311
type: docs
---

Creates a flow from a task operation and treats thrown exceptions as recoverable typed errors.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.attemptTask&#32;<span>operation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `operation` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1">Task</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;exn,&#32;'value</span>&gt;</span></code> |  |

## Remarks

Successful completion returns <code>Exit.Success</code>. <code>OperationCanceledException</code> returns <code>Cause.Interrupt</code>. Other exceptions return <code>Cause.Fail exn</code>.
