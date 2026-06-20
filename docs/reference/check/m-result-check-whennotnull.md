---
title: "Result.Check.whenNotNull"
linkTitle: "whenNotNull"
weight: 2908
---

Keeps the reference when it is not null.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.whenNotNull&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'value</code> | The reference value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;'value&gt;</span></code> | <code>Ok value</code> when non-null; otherwise <code>Error ()</code>. |
