---
title: "Refined.FiniteFloat"
linkTitle: "FiniteFloat"
weight: 1001
type: docs
---

A double-precision float that is neither infinite nor <code>NaN</code>.

## Signature

<div class="fsdocs-usage">
<code>type FiniteFloat</code>
</div>

## Remarks

<p class='fsdocs-para'>
 This type is not for arithmetic. Two finite doubles can sum to infinity, and threading
 a <code>Result</code> through every step costs more than it returns — F# cannot propagate the
 invariant through arithmetic the way a dependent type system would. Unwrap with
 <code>value</code>, compute in plain <code>float</code>, and re-admit the answer. The aggregates
 below do exactly that, failing once at the end rather than at every step.
 </p><p class='fsdocs-para'>
 What it guarantees is that aggregation means something. A single <code>NaN</code> or infinity
 silently destroys a whole aggregate — the sum and the average of
 <code>[ 12.5; 3.0; nan; 8.25 ]</code> are both <code>NaN</code>, with no exception and no obviously
 wrong number. Admitting through this type localises the bad value to the boundary.
 <code>NaN</code> also makes <code>List.contains</code> and <code>List.distinct</code> wrong, because both
 use IEEE equality, under which <code>NaN</code> is not equal to itself.
 </p><p class='fsdocs-para'>
 It is <em>not</em> needed for sorting or for <code>Map</code>, <code>Set</code> and
 <code>Dictionary</code> keys. F# generic comparison already orders <code>NaN</code> consistently —
 <code>compare nan nan</code> is <code>0</code> and <code>NaN</code> sorts first — so those work on plain
 <code>float</code>. What stays broken is a comparison hand-written with <code>&lt;</code> and
 <code>&gt;</code>: it reports <code>NaN</code> equal to every value, which is intransitive and makes
 <code>sortWith</code> return unsorted output without raising.
 </p>


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Finite.fs#L33-33)
