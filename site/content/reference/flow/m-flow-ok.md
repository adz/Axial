---
title: "Flow.ok"
linkTitle: "ok"
weight: 2300
type: docs
---

Creates a successful synchronous flow.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.ok&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'value</code> | The value to wrap in a successful flow. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A flow that always succeeds with the provided value. |
