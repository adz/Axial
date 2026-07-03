---
title: "Refined.Refine.withCheck"
linkTitle: "withCheck"
weight: 2900
---

Builds a refined value by running a reusable check-shaped program before calling the constructor.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.Refine.withCheck&#32;<span>target&#32;check&#32;mapFailures&#32;construct&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `target` | <code>string</code> |  |
| `check` | <code><span>'raw&#32;->&#32;<span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>unit,&#32;<span>'failure&#32;list</span></span>&gt;</span></span></code> |  |
| `mapFailures` | <code><span>string&#32;->&#32;<span>'failure&#32;list</span>&#32;->&#32;<a href="/reference/Axial/axial-refined-refinementerror.html">RefinementError</a></span></code> |  |
| `construct` | <code><span>'raw&#32;->&#32;'refined</span></code> |  |
| `value` | <code>'raw</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'refined,&#32;<a href="/reference/Axial/axial-refined-refinementerror.html">RefinementError</a></span>&gt;</span></code> |  |
