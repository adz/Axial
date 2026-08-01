---
title: "Refined.UnitInterval.inverseLerp"
linkTitle: "inverseLerp"
weight: 2724
type: docs
---


 Returns the proportion a value sits at between two bounds — the inverse of
 <code>lerp</code>. Clamped into range, so it is total. Degenerate bounds, where the two
 are equal, give zero rather than dividing by it.


## Signature

<div class="fsdocs-usage">
<code><span>Refined.UnitInterval.inverseLerp&#32;<span>low&#32;high&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `low` | <code>float</code> |  |
| `high` | <code>float</code> |  |
| `value` | <code>float</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="types/t-refined-unitinterval.md">UnitInterval</a></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/UnitInterval.fs#L109-109)
