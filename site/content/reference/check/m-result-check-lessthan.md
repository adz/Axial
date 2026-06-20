---
title: "Result.Check.lessThan"
linkTitle: "lessThan"
weight: 2803
type: docs
---

Returns success when the actual value is less than the supplied bound.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.lessThan&#32;<span>maximum&#32;actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `maximum` | <code>'a</code> | The exclusive upper bound. |
| `actual` | <code>'a</code> | The value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="/reference/Axial/axial-result-rangefailure-1.html">RangeFailure</a>&lt;'a&gt;</span></span>&gt;</span></code> | <code>Ok ()</code> when the value is less; otherwise a range failure. |
