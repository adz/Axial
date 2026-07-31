---
title: "Refined.FiniteFloat"
linkTitle: "FiniteFloat"
weight: 1010
type: docs
---

A double-precision float that is neither infinite nor <code>NaN</code>.

## Signature

<div class="fsdocs-usage">
<code>type FiniteFloat</code>
</div>

## Remarks

<p class='fsdocs-para'>
 This type does <em>not</em> make arithmetic safe. Two finite doubles can sum or
 multiply to infinity, so <code>add</code>, <code>multiply</code>, and <code>average</code> return
 <code>Result</code>. Only <code>negate</code> and <code>abs</code> are closed.
 </p><p class='fsdocs-para'>
 What it does guarantee is lawful ordering. <code>NaN</code> compares false against every
 value including itself, which makes <code>float</code> violate the reflexivity that sorting,
 binary search, and <code>Map</code> keys depend on — silently, and with results that vary by
 input order. Excluding <code>NaN</code> is what makes comparison total and lawful, which is
 the reason to reach for this type.
 </p>


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Finite.fs#L22-22)
