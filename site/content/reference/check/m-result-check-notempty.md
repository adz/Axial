---
title: "Result.Check.notEmpty"
linkTitle: "notEmpty"
weight: 2504
type: docs
---

Returns success when the sequence is not empty.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.notEmpty&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for non-empty sequences; otherwise <code>Error ()</code>. |
