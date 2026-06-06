---
title: "Check.whenCount"
linkTitle: "whenCount"
weight: 2927
---

Keeps the collection when its count equals the expected count.

## Signature

<div class="fsdocs-usage">
<code><span>Check.whenCount&#32;<span>expected&#32;values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>int</code> | The expected item count. |
| `values` | <code>'collection</code> | The collection to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;'collection&gt;</span></code> | <code>Ok values</code> when the count matches; otherwise <code>Error ()</code>. |
