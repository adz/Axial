---
title: "Check.whenHasDuplicates"
linkTitle: "whenHasDuplicates"
weight: 2928
---

Keeps the collection when it contains duplicate values.

## Signature

<div class="fsdocs-usage">
<code><span>Check.whenHasDuplicates&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code>'collection</code> | The collection to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;'collection&gt;</span></code> | <code>Ok values</code> when a duplicate is found; otherwise <code>Error ()</code>. |
