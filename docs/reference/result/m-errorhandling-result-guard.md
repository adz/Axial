---
title: "ErrorHandling.Result.guard"
linkTitle: "guard"
weight: 2200
---

Lifts a predicate into a type-preserving result guard with the supplied error.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Result.guard&#32;<span>predicate&#32;failure&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `predicate` | <code><span>'input&#32;->&#32;bool</span></code> |  |
| `failure` | <code>'error</code> |  |
| `input` | <code>'input</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'input,&#32;'error</span>&gt;</span></code> |  |
