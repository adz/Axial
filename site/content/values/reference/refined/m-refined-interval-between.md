---
title: "Refined.Interval.between"
linkTitle: "between"
weight: 2300
type: docs
---


 Builds the smallest interval containing both values, ordering them as needed.
 Total — this is the constructor to reach for first.


## Signature

<div class="fsdocs-usage">
<code><span>Refined.Interval.between&#32;<span>first&#32;second</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `first` | <code>'a</code> |  |
| `second` | <code>'a</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="types/t-refined-interval.md">Interval</a>&lt;'a&gt;</span></code> |  |

## Remarks


 Requires a total order. With <code>float</code> or <code>float32</code>, a <code>NaN</code> argument
 cannot be ordered against anything and produces inverted bounds; prefer
 <code>Interval&lt;FiniteFloat&gt;</code> or <code>create</code> there.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Interval.fs#L59-59)
