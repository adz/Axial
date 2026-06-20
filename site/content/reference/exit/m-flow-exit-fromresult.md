---
title: "Flow.Exit.fromResult"
linkTitle: "fromResult"
weight: 2104
type: docs
---

Creates an exit outcome from a standard F# <code>Result</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Exit.fromResult&#32;<span>result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `result` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'v,&#32;'e</span>&gt;</span></code> | The result to convert. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-exit-2.html">Exit</a>&lt;<span>'v,&#32;'e</span>&gt;</span></code> | An exit outcome representing the result. |
