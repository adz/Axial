---
title: "Check.takeNotNull"
linkTitle: "takeNotNull"
weight: 3003
---

Takes the reference when it is not null.

## Signature

<div class="fsdocs-usage">
<code><span>Check.takeNotNull&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'value</code> | The reference value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;'value&gt;</span></code> | <code>Ok value</code> when non-null; otherwise <code>Error ()</code>. |
