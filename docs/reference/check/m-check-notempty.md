---
title: "Check.notEmpty"
linkTitle: "notEmpty"
weight: 2504
---

Returns success when the sequence is not empty.

## Signature

<div class="fsdocs-usage">
<code><span>Check.notEmpty&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for non-empty sequences; otherwise <code>Error ()</code>. |
