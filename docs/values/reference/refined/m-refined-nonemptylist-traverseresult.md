---
title: "Refined.NonEmptyList.traverseResult"
linkTitle: "traverseResult"
weight: 2712
---

 Applies a fallible mapping to every item, accumulating every failure rather than
 stopping at the first.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.NonEmptyList.traverseResult&#32;<span>mapping&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapping` | <code><span>'value&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'result,&#32;<span>'failure&#32;list</span></span>&gt;</span></span></code> |  |
| `input` | <code><span><a href="types/t-refined-nonemptylist.md">NonEmptyList</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span><span><a href="types/t-refined-nonemptylist.md">NonEmptyList</a>&lt;'result&gt;</span>,&#32;<span>'failure&#32;list</span></span>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/NonEmpty.fs#L322-322)
