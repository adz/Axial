---
title: "Flow.Flow.fromAsync"
linkTitle: "fromAsync"
weight: 2308
type: docs
---

Creates a flow from a raw async operation.

**Platform:** Fable compatible

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.fromAsync&#32;<span>operation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `operation` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-control-fsharpasync-1">Async</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> |  |

## Remarks

Thrown exceptions are recorded as defects (<code>Cause.Die</code>), while cancellation is recorded as interruption. Use <code>attemptAsync</code> when expected exceptions should enter the typed error channel.
