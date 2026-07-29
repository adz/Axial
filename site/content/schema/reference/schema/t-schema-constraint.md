---
title: "Schema.Constraint"
linkTitle: "Constraint"
weight: 1113
type: docs
---

 Creates typed Schema constraints and inspects their erased descriptors.
 <example>
 <code>
 let schema = Schema.text |> Schema.constrain (Constraint.maxLength 80)
 let custom = Axial.Check.Constraint.define "named" [] check |> Constraint.fromCheck
 </code>
 </example>

## Signature

<div class="fsdocs-usage">
<code>type Constraint</code>
</div>


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/Constraints.fs#L55-55)
