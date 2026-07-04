---
title: "Flow.Flow.attemptAsync"
linkTitle: "attemptAsync"
weight: 2309
---

Creates a flow from an async operation and treats thrown exceptions as recoverable typed errors.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.attemptAsync&#32;<span>operation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `operation` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpasync-1">Async</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;exn,&#32;'value</span>&gt;</span></code> |  |

## Remarks

Successful completion returns <code>Exit.Success</code>. <code>OperationCanceledException</code> returns <code>Cause.Interrupt</code>. Other exceptions return <code>Cause.Fail exn</code>.
