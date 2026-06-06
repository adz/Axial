---
title: "Check.atMost"
linkTitle: "atMost"
weight: 2805
---

Returns success when the actual value is less than or equal to the supplied bound.

## Signature

<div class="fsdocs-usage">
<code><span>Check.atMost&#32;<span>maximum&#32;actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `maximum` | <code>'a</code> | The inclusive upper bound. |
| `actual` | <code>'a</code> | The value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="t-rangefailure.md">RangeFailure</a>&lt;'a&gt;</span></span>&gt;</span></code> | <code>Ok ()</code> when the value is at most the bound; otherwise a range failure. |
