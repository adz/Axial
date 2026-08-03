---
title: "Result.orError"
linkTitle: "orError"
weight: 2203
---

Replaces whatever error a result carries with the supplied typed error. <code>Ok</code> passes through unchanged.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Result.orError&#32;<span>failure&#32;result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `failure` | <code>'error</code> |  |
| `result` | <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'discardedError</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> |  |

## Remarks

The natural follow-up to <code>okIf</code>/<code>failIf</code>, which fail with <code>unit</code> precisely so the
 reason is chosen here: <code>value |&gt; Result.okIf isValid |&gt; Result.orError MyError</code>. Use
 <code>Result.mapError</code> instead when the existing error carries something worth keeping, as a
 <code>Violation</code> does.


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Result/Result.fs#L78-78)
