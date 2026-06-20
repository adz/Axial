---
title: "Result.Check.whenNull"
linkTitle: "whenNull"
weight: 2909
---

Keeps the reference when it is null.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.whenNull&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'value</code> | The reference value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;'value&gt;</span></code> | <code>Ok null</code> when null; otherwise <code>Error ()</code>. |
