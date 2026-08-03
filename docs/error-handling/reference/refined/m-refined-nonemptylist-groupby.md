---
title: "Refined.NonEmptyList.groupBy"
linkTitle: "groupBy"
weight: 2713
---

 Groups items by a key. Every group is non-empty by construction — a group only
 exists because something fell into it — so the values keep their type rather than
 degrading to a list the caller has to re-check.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.NonEmptyList.groupBy&#32;<span>projection&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `projection` | <code><span>'value&#32;->&#32;'a</span></code> |  |
| `input` | <code><span><a href="types/t-refined-nonemptylist.md">NonEmptyList</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-fsharpmap-2">Map</a>&lt;<span>'a,&#32;<span><a href="types/t-refined-nonemptylist.md">NonEmptyList</a>&lt;'value&gt;</span></span>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/NonEmpty.fs#L299-299)
