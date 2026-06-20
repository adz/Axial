---
title: "Result.Check.hasCount"
linkTitle: "hasCount"
weight: 2701
---

Returns success when the sequence count equals the expected count.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.hasCount&#32;<span>expected&#32;values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>int</code> | The expected item count. |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> when the count matches; otherwise <code>Error ()</code>. |
