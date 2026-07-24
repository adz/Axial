---
title: "Refined.Refine.withCheck"
linkTitle: "withCheck"
weight: 2901
type: docs
---

Runs a <a href="../../check/t-errorhandling-check.md">Check</a> and calls the constructor when the check succeeds.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.Refine.withCheck&#32;<span>target&#32;check&#32;construct&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `target` | <code>string</code> |  |
| `check` | <code><span><a href="../../check/t-errorhandling-check.md">Check</a>&lt;'raw&gt;</span></code> |  |
| `construct` | <code><span>'raw&#32;->&#32;'refined</span></code> |  |
| `value` | <code>'raw</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'refined,&#32;<a href="../types/t-refined-refinementerror.md">RefinementError</a></span>&gt;</span></code> |  |
