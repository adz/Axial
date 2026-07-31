---
title: "Refined.Interval"
linkTitle: "Interval<value>"
weight: 1016
type: docs
---

An inclusive range of ordered values where <code>Lower &lt;= Upper</code>.

## Signature

<div class="fsdocs-usage">
<code>type Interval<'value></code>
</div>

## Type Parameters

| Name |
| --- |
| `value` |

## Record Fields

| Field | Description |
| --- | --- |
| `LowerValue` |  |
| `UpperValue` |  |

## Remarks


 An interval is always inhabited. Emptiness is represented by <code>Interval option</code>,
 which is what <code>intersect</code> returns, rather than by a second type — carrying a
 possibly-empty interval would double every operation without making any of them total.

 The two ends are named for their roles as bounds, not for a traversal: an interval has
 no direction, and <code>between 5 1</code> equals <code>between 1 5</code>. Wire formats that read
 better as <code>start</code>/<code>end</code> choose those field names at the schema, which is
 independent of these members — see <code>RefinedSchemas.dateRange</code>.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Interval.fs#L16-16)
