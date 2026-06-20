---
title: "Flow.Flow.error"
linkTitle: "error"
weight: 2301
type: docs
---

Creates a failing synchronous flow.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.error&#32;<span>failure</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `failure` | <code>'error</code> | The error value to wrap in a failing flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that always fails with the provided error. |
