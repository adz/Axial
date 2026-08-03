---
title: "Refined.Interval"
linkTitle: "Interval<value>"
weight: 1007
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

 The invariant assumes the value type is <em>totally</em> ordered. <code>float</code> and
 <code>float32</code> are not: <code>NaN</code> compares false against everything, so
 <code>between nan x</code> cannot order its arguments and yields an interval whose bounds are
 inverted. Use <code>Interval&lt;FiniteFloat&gt;</code>, which excludes <code>NaN</code> by
 construction, or <code>create</code>, which rejects the pair. This is the same defect
 <a href="t-refined-finitefloat.md">FiniteFloat</a> exists to remove.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Interval.fs#L23-23)
