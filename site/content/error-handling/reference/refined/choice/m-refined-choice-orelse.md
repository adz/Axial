---
title: "Refined.Choice.orElse"
linkTitle: "orElse"
weight: 2600
type: docs
---



## Signature

<div class="fsdocs-usage">
<code><span>Refined.Choice.orElse&#32;<span>leftMap&#32;left&#32;rightMap&#32;right&#32;fallbackError&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `leftMap` | <code><span>'a&#32;->&#32;'b</span></code> |  |
| `left` | <code><span>'c&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'a,&#32;'d</span>&gt;</span></span></code> |  |
| `rightMap` | <code><span>'e&#32;->&#32;'b</span></code> |  |
| `right` | <code><span>'c&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'e,&#32;'f</span>&gt;</span></span></code> |  |
| `fallbackError` | <code>'g</code> |  |
| `input` | <code>'c</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'b,&#32;'g</span>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Refine.fs#L473-473)
