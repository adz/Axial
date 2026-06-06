---
title: "Check.between"
linkTitle: "between"
weight: 2806
type: docs
---

Returns success when the actual value is between the inclusive bounds.

## Signature

<div class="fsdocs-usage">
<code><span>Check.between&#32;<span>minimum&#32;maximum&#32;actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minimum` | <code>'a</code> | The inclusive lower bound. |
| `maximum` | <code>'a</code> | The inclusive upper bound. |
| `actual` | <code>'a</code> | The value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="t-rangefailure.md">RangeFailure</a>&lt;'a&gt;</span></span>&gt;</span></code> | <code>Ok ()</code> when the value is in range; otherwise a range failure. |
