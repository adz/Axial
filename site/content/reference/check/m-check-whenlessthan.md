---
title: "Check.whenLessThan"
linkTitle: "whenLessThan"
weight: 2935
type: docs
---

Keeps the value when it is less than the supplied bound.

## Signature

<div class="fsdocs-usage">
<code><span>Check.whenLessThan&#32;<span>maximum&#32;actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `maximum` | <code>'a</code> | The exclusive upper bound. |
| `actual` | <code>'a</code> | The value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'a,&#32;<span><a href="t-rangefailure.md">RangeFailure</a>&lt;'a&gt;</span></span>&gt;</span></code> | <code>Ok actual</code> when the value is less; otherwise a range failure. |
