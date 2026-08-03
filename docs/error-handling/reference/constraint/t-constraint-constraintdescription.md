---
title: "Constraint.ConstraintDescription"
linkTitle: "ConstraintDescription"
weight: 1003
---


 What a constraint says, as inspectable data.


## Signature

<div class="fsdocs-usage">
<code>type ConstraintDescription</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `Description` | Non-diagnostic prose attached by <code>Constraint.describe</code>, for documentation and inspection. |
| `Expression` | The constraint's logical form. |

## Remarks

<p class='fsdocs-para'>
 This is the read model <code>Constraint.inspect</code> returns and the source every interpreter reads. It is never
 interpreted during execution: a constraint&#39;s closures are composed once at construction.
 </p><p class='fsdocs-para'>
 A description is contextual, not standalone. Atoms are shape-neutral, so an interpreter combines a description
 with the surrounding schema shape — <code>Cardinality.Maximum 5</code> becomes <code>maxLength</code>, <code>maxItems</code>, or
 <code>maxProperties</code> depending on what it is attached to.
 </p><p class='fsdocs-para'>
 An opaque child never erases its portable siblings or parents: the surrounding structure stays inspectable and
 only the opaque node itself declines export.
 </p>


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/ConstraintDescription.fs#L51-51)
