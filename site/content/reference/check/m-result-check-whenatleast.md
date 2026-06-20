---
title: "Result.Check.whenAtLeast"
linkTitle: "whenAtLeast"
weight: 2936
type: docs
---

Keeps the value when it is greater than or equal to the supplied bound.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.whenAtLeast&#32;<span>minimum&#32;actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minimum` | <code>'a</code> | The inclusive lower bound. |
| `actual` | <code>'a</code> | The value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'a,&#32;<span><a href="/reference/Axial/axial-result-rangefailure-1.html">RangeFailure</a>&lt;'a&gt;</span></span>&gt;</span></code> | <code>Ok actual</code> when the value is at least the bound; otherwise a range failure. |
