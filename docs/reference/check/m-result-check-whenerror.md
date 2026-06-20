---
title: "Result.Check.whenError"
linkTitle: "whenError"
weight: 2911
---

Keeps the result when it is <code>Error</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.whenError&#32;<span>result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `result` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The result to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span>&gt;</span></code> | <code>Ok result</code> when the result is <code>Error</code>; otherwise <code>Error ()</code>. |
