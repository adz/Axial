---
title: "DataPathSegment"
linkTitle: "DataPathSegment"
weight: 1001
---

A segment in a structured data path.

## Signature

<div class="fsdocs-usage">
<code>type DataPathSegment</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `Name` | A named source field or object member. |
| `Index` | A zero-based collection index. |

## Remarks

<p class='fsdocs-para'>
 Structured data paths address boundary data by source field names and zero-based collection indexes. They are intentionally
 separate from diagnostics graphs, but can be lowered to diagnostics paths when schema input errors are interpreted.
 </p>
