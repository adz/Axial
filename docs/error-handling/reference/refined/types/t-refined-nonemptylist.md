---
title: "Refined.NonEmptyList"
linkTitle: "NonEmptyList<value>"
weight: 1004
---

A list that contains at least one item.

## Signature

<div class="fsdocs-usage">
<code>type NonEmptyList<'value></code>
</div>

## Type Parameters

| Name |
| --- |
| `value` |

## Union Cases

| Case | Description |
| --- | --- |
| `NonEmpty` |  |

## Remarks


 The case is public: non-emptiness is carried by the representation rather than by a
 checked constructor, so <code>head</code>, <code>last</code>, <code>reduce</code>, <code>min</code>, and
 <code>max</code> are total and pattern matching is available to callers.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/NonEmpty.fs#L11-11)
