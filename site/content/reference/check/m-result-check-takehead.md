---
title: "Result.Check.takeHead"
linkTitle: "takeHead"
weight: 3005
type: docs
---

Takes the first item from a sequence when it is not empty.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.takeHead&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;'value&gt;</span></code> | <code>Ok value</code> for the first item; otherwise <code>Error ()</code>. |
