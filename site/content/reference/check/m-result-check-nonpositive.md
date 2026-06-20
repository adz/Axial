---
title: "Result.Check.nonPositive"
linkTitle: "nonPositive"
weight: 2810
type: docs
---

Returns success when the numeric value is less than or equal to zero.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.nonPositive&#32;<span>actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `actual` | <code>^a</code> | The value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="/reference/Axial/axial-result-rangefailure-1.html">RangeFailure</a>&lt;^a&gt;</span></span>&gt;</span></code> | <code>Ok ()</code> when the value is non-positive; otherwise a range failure. |
