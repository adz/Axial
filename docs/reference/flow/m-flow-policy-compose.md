---
title: "Flow.Policy.compose"
linkTitle: "compose"
weight: 2405
---

Composes two policies left to right.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Policy.compose&#32;<span>first&#32;second&#32;environment&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `first` | <code><span><a href="/reference/Axial/axial-flow-policy-4.html">Policy</a>&lt;<span>'env,&#32;'error,&#32;'input,&#32;'middle</span>&gt;</span></code> |  |
| `second` | <code><span><a href="/reference/Axial/axial-flow-policy-4.html">Policy</a>&lt;<span>'env,&#32;'error,&#32;'middle,&#32;'output</span>&gt;</span></code> |  |
| `environment` | <code>'env</code> |  |
| `input` | <code>'input</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'output,&#32;'error</span>&gt;</span></code> |  |
