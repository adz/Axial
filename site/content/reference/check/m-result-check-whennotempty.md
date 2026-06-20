---
title: "Result.Check.whenNotEmpty"
linkTitle: "whenNotEmpty"
weight: 2912
type: docs
---

Keeps the collection when it is not empty.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.whenNotEmpty&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code>'collection</code> | The collection to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;'collection&gt;</span></code> | <code>Ok values</code> for non-empty collections; otherwise <code>Error ()</code>. |
