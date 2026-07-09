---
title: "ErrorHandling.Result.failIf"
linkTitle: "failIf"
weight: 2202
---

Keeps the input value when the predicate does not hold, or returns the supplied error.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Result.failIf&#32;<span>predicate&#32;input</span></span></code>
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

## Remarks

The inverse of <code>okIf</code>: fails when the predicate is true, succeeds otherwise.
