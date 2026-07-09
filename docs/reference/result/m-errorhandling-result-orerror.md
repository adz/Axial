---
title: "ErrorHandling.Result.orError"
linkTitle: "orError"
weight: 2203
---

Replaces whatever error a result carries with the supplied typed error. <code>Ok</code> passes through unchanged.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Result.orError&#32;<span>failure&#32;result</span></span></code>
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

The natural follow-up to <code>okIf</code>/<code>failIf</code>, and to any <code>Check</code> call whose
 <code>CheckFailure list</code> should become a domain error: <code>value |&gt; Check.String.present |&gt; Result.orError MyError</code>.
