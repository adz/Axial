---
title: "Exit.toResult"
linkTitle: "toResult"
weight: 2105
---

Converts an exit outcome to a standard F# <code>Result</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Exit.toResult&#32;<span>exit</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `exit` | <code><span><a href="t-exit.md">Exit</a>&lt;<span>'v,&#32;'e</span>&gt;</span></code> | The exit outcome to convert. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'v,&#32;'e</span>&gt;</span></code> | A <code>Result</code> representing the successful value or the domain failure. |
