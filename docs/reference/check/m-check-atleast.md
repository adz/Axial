---
title: "Check.atLeast"
linkTitle: "atLeast"
weight: 2804
---

Returns success when the actual value is greater than or equal to the supplied bound.

## Signature

<div class="fsdocs-usage">
<code><span>Check.atLeast&#32;<span>minimum&#32;actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minimum` | <code>'a</code> | The inclusive lower bound. |
| `actual` | <code>'a</code> | The value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="t-rangefailure.md">RangeFailure</a>&lt;'a&gt;</span></span>&gt;</span></code> | <code>Ok ()</code> when the value is at least the bound; otherwise a range failure. |
