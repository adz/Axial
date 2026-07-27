---
title: "Refined.Refinement.defineWithCheck"
linkTitle: "defineWithCheck"
weight: 2703
type: docs
---

 Defines a metadata-free refinement from an executable check.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.Refinement.defineWithCheck&#32;<span>check&#32;construct&#32;project</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `check` | <code><span><a href="../check/t-check-check.md">Check</a>&lt;'underlying&gt;</span></code> |  |
| `construct` | <code><span>'underlying&#32;->&#32;'refined</span></code> |  |
| `project` | <code><span>'refined&#32;->&#32;'underlying</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-refined-refinement.md">Refinement</a>&lt;<span>'underlying,&#32;'refined</span>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Refine.fs#L324-324)
