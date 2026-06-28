---
title: "Flow.Policy.optional"
linkTitle: "optional"
weight: 2406
---

Runs a policy only when the environment predicate is true; otherwise returns the input unchanged.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Policy.optional&#32;<span>enabled&#32;policy&#32;environment&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `enabled` | <code><span>'env&#32;->&#32;bool</span></code> |  |
| `policy` | <code><span><a href="/reference/Axial/axial-flow-policy-4.html">Policy</a>&lt;<span>'env,&#32;'error,&#32;'input,&#32;'input</span>&gt;</span></code> |  |
| `environment` | <code>'env</code> |  |
| `input` | <code>'input</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'input,&#32;'error</span>&gt;</span></code> |  |
