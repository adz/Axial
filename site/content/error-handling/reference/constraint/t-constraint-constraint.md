---
title: "Constraint"
linkTitle: "Constraint<value>"
weight: 1000
type: docs
---


 A reusable description of valid values, coupled to the closures that execute it.


## Signature

<div class="fsdocs-usage">
<code>type Constraint<'value></code>
</div>

## Type Parameters

| Name |
| --- |
| `value` |

## Remarks

<p class='fsdocs-para'>
 One constraint value serves direct checking, refined-value admission, Schema, documentation, and export. There
 is no separate check type: <code>check</code> is the operation, <code>Constraint</code> is the noun.
 </p><p class='fsdocs-para'>
 Both closures are retained deliberately. They are not duplicates of one rule: <code>test</code> over a conjunction may
 stop at the first failing child, while <code>check</code> must run every child to accumulate. Interpreted atoms and
 <code>custom</code> predicates therefore have a Boolean path that does no violation work, and combinators preserve
 that property when every child has it. A <code>customWith</code> constraint supplies only a violation-returning
 callback, so its <code>test</code> runs that callback and discards the error.
 </p><p class='fsdocs-para'>
 The description is never interpreted during execution. Closures are composed once, at construction.
 </p>


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Constraint.fs#L26-26)
