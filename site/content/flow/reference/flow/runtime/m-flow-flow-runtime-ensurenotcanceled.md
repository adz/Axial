---
title: "Flow.Flow.Runtime.ensureNotCanceled"
linkTitle: "ensureNotCanceled"
weight: 2102
type: docs
---

Returns a typed error immediately when the runtime token is already canceled.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.Runtime.ensureNotCanceled&#32;<span>canceledError</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `canceledError` | <code>'error</code> | The error to return when cancellation has been requested. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'error,&#32;unit</span>&gt;</span></code> | A flow that succeeds with unit when cancellation has not been requested. |
