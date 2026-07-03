---
title: "ErrorHandling.Check.String.minLength"
linkTitle: "minLength"
weight: 2201
type: docs
---

Requires an already parsed string value to have at least the supplied length. Null fails with an unknown actual length.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Check.String.minLength&#32;<span>minimum&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minimum` | <code>int</code> |  |
| `value` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="/reference/Axial/axial-errorhandling-checkfailure.html">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |
