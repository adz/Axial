---
title: "Check.contains"
linkTitle: "contains"
weight: 2700
---

Returns success when the sequence contains the expected value.

## Signature

<div class="fsdocs-usage">
<code><span>Check.contains&#32;<span>expected&#32;values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>'value</code> | The value to search for. |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> when the value is present; otherwise <code>Error ()</code>. |
