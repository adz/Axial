---
title: "ErrorHandling.Result.bind"
linkTitle: "bind"
weight: 2104
---

Binds a result to the next fail-fast operation.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Result.bind&#32;<span>binder&#32;result</span></span></code>
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
