---
title: "Flow.Policy.pure"
linkTitle: "pure"
weight: 2401
type: docs
---

Lifts a pure result-returning function and maps its error into the workflow error type.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Policy.pure&#32;<span>operation&#32;mapError&#32;arg3&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `operation` | <code><span>'input&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'output,&#32;'innerError</span>&gt;</span></span></code> |  |
| `mapError` | <code><span>'innerError&#32;->&#32;'error</span></code> |  |
| `arg2` | <code>'env</code> |  |
| `input` | <code>'input</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'output,&#32;'error</span>&gt;</span></code> |  |
