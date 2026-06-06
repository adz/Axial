---
title: "Check.whenNonPositive"
linkTitle: "whenNonPositive"
weight: 2942
type: docs
---

Keeps the numeric value when it is less than or equal to zero.

## Signature

<div class="fsdocs-usage">
<code><span>Check.whenNonPositive&#32;<span>actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `actual` | <code>^a</code> | The value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>^a,&#32;<span><a href="t-rangefailure.md">RangeFailure</a>&lt;^a&gt;</span></span>&gt;</span></code> | <code>Ok actual</code> when the value is non-positive; otherwise a range failure. |
