---
title: "Constraint.ConstraintAtom"
linkTitle: "ConstraintAtom"
weight: 1005
---


 One interpreted primitive: the complete semantic identity of a built-in constraint.


## Signature

<div class="fsdocs-usage">
<code>type ConstraintAtom</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `PresenceAtom` | A presence rule. |
| `CardinalityAtom` | A text length or collection count rule. |
| `RelationAtom` | An ordering or equality rule. |
| `MembershipAtom` | A membership rule. |
| `UniquenessAtom` | A no-duplicates rule. The duplicate itself appears as the violation's actual value. |
| `FormatAtom` | A built-in text format rule. |
| `NumberAtom` | A numeric-property rule. |

## Remarks


 An interpreted constructor builds exactly one atom and places that same value in both its description and any
 violation it produces, so a primitive&#39;s identity and its failure cannot drift. Atoms are shape-neutral; an
 interpreter combines an atom with the surrounding schema shape to decide what it lowers to.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/ConstraintAtom.fs#L119-119)
