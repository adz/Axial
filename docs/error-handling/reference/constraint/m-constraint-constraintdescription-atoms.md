---
title: "Constraint.ConstraintDescription.atoms"
linkTitle: "atoms"
weight: 3001
---

Every interpreted primitive reachable without crossing an opacity boundary, in authored order.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.ConstraintDescription.atoms&#32;<span>description</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `description` | <code><a href="t-constraint-constraintdescription.md">ConstraintDescription</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-constraint-constraintatom.md">ConstraintAtom</a>&#32;list</span></code> |  |

## Remarks


 Use this only where dropping an unexportable sibling stays sound. Dropping a conjunct weakens a conjunction
 and dropping a disjunct strengthens a disjunction, so an interpreter that claims enforcement must consult
 the whole expression rather than this projection.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/ConstraintDescription.fs#L90-90)
