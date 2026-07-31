---
title: "Refined.UnitInterval"
linkTitle: "UnitInterval"
weight: 1012
type: docs
---

A finite double between zero and one inclusive.

## Signature

<div class="fsdocs-usage">
<code>type UnitInterval</code>
</div>

## Remarks


 The only type in this package closed under multiplication: a product of two values in
 <code>[0, 1]</code> is always in <code>[0, 1]</code>, with no overflow to guard against. It is
 <em>not</em> closed under addition — <code>0.7 + 0.7</code> leaves the range — so <code>add</code>
 is deliberately absent in favour of <code>saturatingAdd</code> and <code>complement</code>.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/UnitInterval.fs#L13-13)
