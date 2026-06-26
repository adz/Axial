---
title: "Flow.Flow.attemptValueTask"
linkTitle: "attemptValueTask"
weight: 2313
---

Creates a flow from a value task operation and treats thrown exceptions as recoverable typed errors.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.attemptValueTask&#32;<span>operation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `operation` | <code><span><a href="https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1">ValueTask</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;exn,&#32;'value</span>&gt;</span></code> |  |

## Remarks

Successful completion returns <code>Exit.Success</code>. <code>OperationCanceledException</code> returns <code>Cause.Interrupt</code>. Other exceptions return <code>Cause.Fail exn</code>.
