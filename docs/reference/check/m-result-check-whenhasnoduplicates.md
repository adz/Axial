---
title: "Result.Check.whenHasNoDuplicates"
linkTitle: "whenHasNoDuplicates"
weight: 2929
---

Keeps the collection when it contains no duplicate values.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.whenHasNoDuplicates&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code>'collection</code> | The collection to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;'collection&gt;</span></code> | <code>Ok values</code> when all values are unique; otherwise <code>Error ()</code>. |
