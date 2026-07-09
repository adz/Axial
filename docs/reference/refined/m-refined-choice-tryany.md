---
title: "Refined.Choice.tryAny"
linkTitle: "tryAny"
weight: 2701
---

Tries parser strategies in order and returns the first success.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.Choice.tryAny&#32;<span>fallbackError&#32;strategies&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fallbackError` | <code>'error</code> |  |
| `strategies` | <code><span><span>(<span>'raw&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'output,&#32;'error</span>&gt;</span></span>)</span>&#32;seq</span></code> |  |
| `input` | <code>'raw</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'output,&#32;'error</span>&gt;</span></code> |  |
