---
title: "Flow.Runtime.ensureNotCanceled"
linkTitle: "ensureNotCanceled"
---

Returns a typed error immediately when the runtime token is already canceled.

## Signature

<div class="fsdocs-usage">
<code><span>Runtime.ensureNotCanceled&#32;<span>canceledError</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `canceledError` | <code>'error</code> | The error to return when cancellation has been requested. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;unit</span>&gt;</span></code> | A flow that succeeds with unit when cancellation has not been requested. |

