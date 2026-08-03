---
title: "Refined.DistinctList.toSet"
linkTitle: "toSet"
weight: 2719
type: docs
---


 Builds a set. Total and lossless — this is the operation that justifies the type,
 because distinct items always produce a set of the same size, while
 <code>Set.ofList</code> on an ordinary list silently collapses duplicates.


## Signature

<div class="fsdocs-usage">
<code><span>Refined.DistinctList.toSet&#32;<span>input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `input` | <code><span><a href="types/t-refined-distinctlist.md">DistinctList</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-fsharpset-1">Set</a>&lt;'value&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Refine.fs#L185-185)
