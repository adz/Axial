---
title: "ErrorHandling.Check.String.exactLength"
linkTitle: "exactLength"
weight: 2204
---

Requires an already parsed string value to have exactly the supplied length. Null fails with an unknown actual length.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Check.String.exactLength&#32;<span>expected&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>int</code> |  |
| `value` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="/reference/Axial/axial-errorhandling-checkfailure.html">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |
