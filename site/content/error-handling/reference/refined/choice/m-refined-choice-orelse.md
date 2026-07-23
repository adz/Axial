---
title: "Refined.Choice.orElse"
linkTitle: "orElse"
weight: 2700
type: docs
---

Tries the left parser first, then the right parser, mapping either success into your output type.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.Choice.orElse&#32;<span>leftMap&#32;left&#32;rightMap&#32;right&#32;fallbackError&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `leftMap` | <code><span>'left&#32;->&#32;'output</span></code> |  |
| `left` | <code><span>'raw&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'left,&#32;'error</span>&gt;</span></span></code> |  |
| `rightMap` | <code><span>'right&#32;->&#32;'output</span></code> |  |
| `right` | <code><span>'raw&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'right,&#32;'error</span>&gt;</span></span></code> |  |
| `fallbackError` | <code>'error</code> |  |
| `input` | <code>'raw</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'output,&#32;'error</span>&gt;</span></code> |  |
