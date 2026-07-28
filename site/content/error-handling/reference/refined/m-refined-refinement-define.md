---
title: "Refined.Refinement.define"
linkTitle: "define"
weight: 2701
type: docs
---

 Defines a refinement from one portable constraint.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.Refinement.define&#32;<span>constraint'&#32;construct&#32;project</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `constraint'` | <code><span><a href="../check/t-check-constraint.md">Constraint</a>&lt;'underlying&gt;</span></code> |  |
| `construct` | <code><span>'underlying&#32;->&#32;'refined</span></code> |  |
| `project` | <code><span>'refined&#32;->&#32;'underlying</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-refined-refinement.md">Refinement</a>&lt;<span>'underlying,&#32;'refined</span>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Refine.fs#L309-309)
