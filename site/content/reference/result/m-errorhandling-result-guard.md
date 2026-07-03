---
title: "ErrorHandling.Result.guard"
linkTitle: "guard"
weight: 2201
type: docs
---

Runs a value check and keeps the original input when it succeeds.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Result.guard&#32;<span>check&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `check` | <code><span><a href="/reference/Axial/axial-errorhandling-check-1.html">Check</a>&lt;'input&gt;</span></code> |  |
| `input` | <code>'input</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'input,&#32;<span><a href="/reference/Axial/axial-errorhandling-checkfailure.html">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |
