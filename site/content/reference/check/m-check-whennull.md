---
title: "Check.whenNull"
linkTitle: "whenNull"
weight: 2909
type: docs
---

Keeps the reference when it is null.

## Signature

<div class="fsdocs-usage">
<code><span>Check.whenNull&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'value</code> | The reference value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;'value&gt;</span></code> | <code>Ok null</code> when null; otherwise <code>Error ()</code>. |
