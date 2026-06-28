---
title: "Flow.Policy.context"
linkTitle: "context"
weight: 2403
---

Lifts an environment-aware result-returning function and maps its error into the workflow error type.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Policy.context&#32;<span>operation&#32;mapError&#32;environment&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `operation` | <code><span>'env&#32;->&#32;'input&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'output,&#32;'innerError</span>&gt;</span></span></code> |  |
| `mapError` | <code><span>'innerError&#32;->&#32;'error</span></code> |  |
| `environment` | <code>'env</code> |  |
| `input` | <code>'input</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'output,&#32;'error</span>&gt;</span></code> |  |
