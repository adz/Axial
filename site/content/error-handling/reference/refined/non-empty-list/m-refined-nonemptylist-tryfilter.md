---
title: "Refined.NonEmptyList.tryFilter"
linkTitle: "tryFilter"
weight: 2812
type: docs
---

Filters the list and re-certifies that at least one item remains.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.NonEmptyList.tryFilter&#32;<span>predicate&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `predicate` | <code><span>'a&#32;->&#32;bool</span></code> |  |
| `input` | <code><span><a href="../types/t-refined-nonemptylist.md">NonEmptyList</a>&lt;'a&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span><span><a href="../types/t-refined-nonemptylist.md">NonEmptyList</a>&lt;'a&gt;</span>,&#32;<a href="../types/t-refined-refinementerror.md">RefinementError</a></span>&gt;</span></code> |  |
