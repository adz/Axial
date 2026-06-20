---
title: "Result.Check.positive"
linkTitle: "positive"
weight: 2807
---

Returns success when the numeric value is greater than zero.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.positive&#32;<span>actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `actual` | <code>^a</code> | The value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="/reference/Axial/axial-result-rangefailure-1.html">RangeFailure</a>&lt;^a&gt;</span></span>&gt;</span></code> | <code>Ok ()</code> when the value is positive; otherwise a range failure. |
