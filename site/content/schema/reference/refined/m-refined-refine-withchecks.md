---
title: "Refined.Refine.withChecks"
linkTitle: "withChecks"
weight: 2901
type: docs
---

Builds a refined value by running every supplied <a href="../check/t-errorhandling-check.md">Check</a> program
 before calling the constructor, accumulating all failures via <code>Check.all</code>.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.Refine.withChecks&#32;<span>target&#32;checks&#32;construct&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `target` | <code>string</code> |  |
| `checks` | <code><span><span><a href="../check/t-errorhandling-check.md">Check</a>&lt;'raw&gt;</span>&#32;list</span></code> |  |
| `construct` | <code><span>'raw&#32;->&#32;'refined</span></code> |  |
| `value` | <code>'raw</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'refined,&#32;<a href="t-refined-refinementerror.md">RefinementError</a></span>&gt;</span></code> |  |
