---
title: "ErrorHandling.Check.all"
linkTitle: "all"
weight: 2100
type: docs
---

Combines checks conjunctively by running every check against the value and accumulating all failures. An empty list succeeds.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Check.all&#32;<span>checks&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `checks` | <code><span><span><a href="/reference/Axial/axial-errorhandling-check-1.html">Check</a>&lt;'value&gt;</span>&#32;list</span></code> |  |
| `value` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="/reference/Axial/axial-errorhandling-checkfailure.html">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |
