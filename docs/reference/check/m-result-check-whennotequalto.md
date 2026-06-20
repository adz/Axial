---
title: "Result.Check.whenNotEqualTo"
linkTitle: "whenNotEqualTo"
weight: 2925
---

Keeps the actual value when it does not equal the expected value.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.whenNotEqualTo&#32;<span>expected&#32;actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>'value</code> | The value that should not match. |
| `actual` | <code>'value</code> | The actual value. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;'value&gt;</span></code> | <code>Ok actual</code> when values differ; otherwise <code>Error ()</code>. |
