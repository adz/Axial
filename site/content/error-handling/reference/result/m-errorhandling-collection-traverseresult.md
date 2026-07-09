---
title: "ErrorHandling.Collection.traverseResult"
linkTitle: "traverseResult"
weight: 2400
type: docs
---

Maps each value with a result-returning function, stopping at the first error.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Collection.traverseResult&#32;<span>mapping&#32;values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `mapping` | <code><span>'input&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'output,&#32;'error</span>&gt;</span></span></code> |  |
| `values` | <code><span>'input&#32;seq</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span><span>'output&#32;list</span>,&#32;'error</span>&gt;</span></code> |  |
