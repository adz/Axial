---
title: "Refined.NonEmptyArray"
linkTitle: "NonEmptyArray<value>"
weight: 1005
type: docs
---

An array that contains at least one item.

## Signature

<div class="fsdocs-usage">
<code>type NonEmptyArray<'value></code>
</div>

## Type Parameters

| Name |
| --- |
| `value` |

## Remarks


 Unlike <a href="t-refined-nonemptylist.md">NonEmptyList</a> this stays smart-constructed. A
 structural head-and-tail representation would forfeit contiguous storage and indexed
 access, which are the reasons to choose an array in the first place.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/NonEmpty.fs#L41-41)
