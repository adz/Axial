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
| `checks` | <code><span><span><a href="t-errorhandling-check.md">Check</a>&lt;'value&gt;</span>&#32;list</span></code> |  |
| `value` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;<span><a href="../result/errors/t-errorhandling-checkfailure.md">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |

## Remarks


 F# visits list elements from left to right. When the first check is a type-directed inline check such as
 <code>Check.present</code>, declare the composed program&#39;s value type so the compiler can select its SRTP overload:
 <code>let requiredName : Check&lt;string&gt; = Check.all [ Check.present; Check.lengthBetween 2 40 ]</code>.
