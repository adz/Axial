---
title: "Check.whenEqualTo"
linkTitle: "whenEqualTo"
weight: 2924
type: docs
---

Keeps the actual value when it equals the expected value.

## Signature

<div class="fsdocs-usage">
<code><span>Check.whenEqualTo&#32;<span>expected&#32;actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>'value</code> | The expected value. |
| `actual` | <code>'value</code> | The actual value. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;'value&gt;</span></code> | <code>Ok actual</code> when equal; otherwise <code>Error ()</code>. |
