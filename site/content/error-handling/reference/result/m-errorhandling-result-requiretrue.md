---
title: "ErrorHandling.Result.requireTrue"
linkTitle: "requireTrue"
weight: 2200
type: docs
---

Returns <code>Ok ()</code> when the condition is true, or the supplied error when it is false.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Result.requireTrue&#32;<span>failure&#32;condition</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `failure` | <code>'error</code> |  |
| `condition` | <code>bool</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;'error</span>&gt;</span></code> |  |

## Remarks

The condition is already computed and stands alone, so there is no subject value to preserve on
 success. Use <code>okIf</code>/<code>failIf</code> instead when the value under test should flow through.
