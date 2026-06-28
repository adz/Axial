---
title: "ErrorHandling.Result.fromPredicate"
linkTitle: "fromPredicate"
weight: 2202
type: docs
---

Lifts a predicate into a unit-error result.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Result.fromPredicate&#32;<span>predicate&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `predicate` | <code><span>'input&#32;->&#32;bool</span></code> |  |
| `input` | <code>'input</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'input,&#32;unit</span>&gt;</span></code> |  |
