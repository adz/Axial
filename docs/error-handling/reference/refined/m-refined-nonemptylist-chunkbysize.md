---
title: "Refined.NonEmptyList.chunkBySize"
linkTitle: "chunkBySize"
weight: 2715
---


 Splits into consecutive runs of the given size. Total: a size below one is treated
 as one, where <code>List.chunkBySize</code> raises, and both the outer list and every
 chunk stay non-empty.


## Signature

<div class="fsdocs-usage">
<code><span>Refined.NonEmptyList.chunkBySize&#32;<span>size&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `size` | <code>int</code> |  |
| `input` | <code><span><a href="types/t-refined-nonemptylist.md">NonEmptyList</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="types/t-refined-nonemptylist.md">NonEmptyList</a>&lt;<span><a href="types/t-refined-nonemptylist.md">NonEmptyList</a>&lt;'value&gt;</span>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/NonEmpty.fs#L310-310)
