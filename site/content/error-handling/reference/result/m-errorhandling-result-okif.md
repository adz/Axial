---
title: "ErrorHandling.Result.okIf"
linkTitle: "okIf"
weight: 2201
type: docs
---

Keeps the input value when the predicate holds, or returns the supplied error.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Result.okIf&#32;<span>predicate&#32;input</span></span></code>
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

Mirrors <code>Option.filter</code>: predicate first, subject piped last. The error is attached
 separately with <code>orError</code> so this stays a pure filter, same shape as its <code>Option</code> counterpart.
