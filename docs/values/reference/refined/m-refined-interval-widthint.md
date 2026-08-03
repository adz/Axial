---
title: "Refined.Interval.widthInt"
linkTitle: "widthInt"
weight: 2305
---


 Returns the distance between the bounds. Total, and widened to 64 bits because the
 width of <code>Int32.MinValue .. Int32.MaxValue</code> does not fit an <code>int</code>.


## Signature

<div class="fsdocs-usage">
<code><span>Refined.Interval.widthInt&#32;<span>interval</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `interval` | <code><span><a href="types/t-refined-interval.md">Interval</a>&lt;int&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>int64</code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Interval.fs#L165-165)
