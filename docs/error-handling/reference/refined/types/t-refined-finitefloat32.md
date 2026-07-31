---
title: "Refined.FiniteFloat32"
linkTitle: "FiniteFloat32"
weight: 1002
---

A single-precision float that is neither infinite nor <code>NaN</code>.

## Signature

<div class="fsdocs-usage">
<code>type FiniteFloat32</code>
</div>

## Remarks


 Carries the same guarantee as <a href="t-refined-finitefloat.md">FiniteFloat</a> — lawful
 ordering — for code that stores single precision. It has no canonical wire schema,
 because JSON has no single-precision number: widen with <code>toFiniteFloat</code> at a
 boundary, or supply a schema explicitly.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Finite.fs#L73-73)
