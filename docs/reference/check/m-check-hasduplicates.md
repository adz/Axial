---
title: "Check.hasDuplicates"
linkTitle: "hasDuplicates"
weight: 2702
---

Returns success when the sequence contains duplicate values.

## Signature

<div class="fsdocs-usage">
<code><span>Check.hasDuplicates&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> when a duplicate is found; otherwise <code>Error ()</code>. |
