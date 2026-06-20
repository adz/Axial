---
title: "Result.Check.hasNoDuplicates"
linkTitle: "hasNoDuplicates"
weight: 2703
type: docs
---

Returns success when the sequence contains no duplicate values.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.hasNoDuplicates&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> when all values are unique; otherwise <code>Error ()</code>. |
