---
title: "Refined.Bounded.clamp"
linkTitle: "clamp"
weight: 2828
type: docs
---

 Restricts a value into the bounds. Total — this is the constructor to reach for
 first, because an out-of-range input has an obvious correct answer.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.Bounded.clamp&#32;<span>bounds&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `bounds` | <code><span><a href="types/t-refined-interval.md">Interval</a>&lt;'a&gt;</span></code> |  |
| `value` | <code>'a</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="types/t-refined-bounded.md">Bounded</a>&lt;'a&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Bounded.fs#L39-39)
