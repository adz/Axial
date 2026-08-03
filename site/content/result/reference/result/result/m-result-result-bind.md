---
title: "Result.bind"
linkTitle: "bind"
weight: 2004
type: docs
---

Binds a result to the next fail-fast operation.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Result.bind&#32;<span>binder&#32;result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `binder` | <code><span>'a&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'b,&#32;'c</span>&gt;</span></span></code> |  |
| `result` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'a,&#32;'c</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'b,&#32;'c</span>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Result/Result.fs#L27-27)
