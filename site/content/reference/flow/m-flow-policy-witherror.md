---
title: "Flow.Policy.withError"
linkTitle: "withError"
weight: 2402
type: docs
---

Lifts a pure result-returning function and replaces any error with a fixed workflow error.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Policy.withError&#32;<span>operation&#32;error</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `operation` | <code><span>'input&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'output,&#32;'innerError</span>&gt;</span></span></code> |  |
| `error` | <code>'error</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-policy-4.html">Policy</a>&lt;<span>'env,&#32;'error,&#32;'input,&#32;'output</span>&gt;</span></code> |  |
