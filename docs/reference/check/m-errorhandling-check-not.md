---
title: "ErrorHandling.Check.``not``"
linkTitle: "``not``"
weight: 2102
---

Inverts a check. A successful inner check becomes a custom-code failure, while any failed inner check succeeds.

## Signature

<div class="fsdocs-usage">
<code><span>ErrorHandling.Check.``not``&#32;<span>check&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `check` | <code><span><a href="/reference/Axial/axial-errorhandling-check-1.html">Check</a>&lt;'value&gt;</span></code> |  |
| `value` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span><a href="/reference/Axial/axial-errorhandling-checkfailure.html">CheckFailure</a>&#32;list</span></span>&gt;</span></code> |  |
