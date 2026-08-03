---
title: "Refined.UnitInterval.complement"
linkTitle: "complement"
weight: 2721
---

Returns the distance to one. Total and closed.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.UnitInterval.complement&#32;<span>input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `input` | <code><a href="types/t-refined-unitinterval.md">UnitInterval</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="types/t-refined-unitinterval.md">UnitInterval</a></code> |  |

## Remarks


 An involution only up to floating-point rounding: the round trip is exact for
 dyadic values such as <code>0.25</code>, but <code>1 - (1 - 0.3)</code> is not <code>0.3</code> in
 IEEE 754. Compare with a tolerance rather than for equality.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/UnitInterval.fs#L71-71)
