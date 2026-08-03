---
title: "Refined.NonEmptyList.tryFilter"
linkTitle: "tryFilter"
weight: 2717
---

Filters the items, returning <code>None</code> when nothing survives.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.NonEmptyList.tryFilter&#32;<span>predicate&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `predicate` | <code><span>'a&#32;->&#32;bool</span></code> |  |
| `input` | <code><span><a href="types/t-refined-nonemptylist.md">NonEmptyList</a>&lt;'a&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><span><a href="types/t-refined-nonemptylist.md">NonEmptyList</a>&lt;'a&gt;</span>&#32;option</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/NonEmpty.fs#L274-274)
