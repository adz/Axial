---
title: "Result.Check.whenNegative"
linkTitle: "whenNegative"
weight: 2941
type: docs
---

Keeps the numeric value when it is less than zero.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.whenNegative&#32;<span>actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `actual` | <code>^a</code> | The value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>^a,&#32;<span><a href="/reference/Axial/axial-result-rangefailure-1.html">RangeFailure</a>&lt;^a&gt;</span></span>&gt;</span></code> | <code>Ok actual</code> when the value is negative; otherwise a range failure. |
