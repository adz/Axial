---
title: "Refined.DistinctList.toMap"
linkTitle: "toMap"
weight: 2718
---


 Builds a map from a distinct list of pairs, failing when two pairs share a key.


## Signature

<div class="fsdocs-usage">
<code><span>Refined.DistinctList.toMap&#32;<span>input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `input` | <code><span><a href="types/t-refined-distinctlist.md">DistinctList</a>&lt;<span>'key&#32;*&#32;'value</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-fsharpmap-2">Map</a>&lt;<span>'key,&#32;'value</span>&gt;</span>,&#32;<a href="../result/errors/t-constraint-violation.md">Violation</a></span>&gt;</span></code> |  |

## Remarks


 Distinctness holds over whole pairs, not over keys: <code>[ 1, &quot;a&quot;; 1, &quot;b&quot; ]</code> is a
 legitimate <code>DistinctList</code> whose entries would collide in a map. The check is
 what makes the conversion lossless — <code>Map.ofList</code> would silently keep one.
 For the unconditional guarantee use <code>toSet</code>, where distinct elements always
 produce a set of the same size.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Refine.fs#L171-171)
