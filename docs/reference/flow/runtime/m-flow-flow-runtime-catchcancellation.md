---
title: "Flow.Flow.Runtime.catchCancellation"
linkTitle: "catchCancellation"
weight: 2101
---

Catches <a href="https://learn.microsoft.com/dotnet/api/operationcanceledexception">OperationCanceledException</a> raised by a flow and converts it into a typed error.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.Runtime.catchCancellation&#32;<span>handler&#32;flow</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `handler` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.operationcanceledexception">OperationCanceledException</a>&#32;->&#32;'error</span></code> | Maps the cancellation exception into the workflow error type. |
| `flow` | <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | The source flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that turns thrown cancellation into <code>Cause.Fail</code>. |

## Remarks


 This handles cancellation exceptions thrown during execution. A flow that has already returned
 <code>Cause.Interrupt</code> remains interrupted.
