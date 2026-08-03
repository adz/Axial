---
title: "Refined.NonEmptyList.zip"
linkTitle: "zip"
weight: 2715
type: docs
---


 Pairs items positionally, truncating to the shorter input. Total — unlike
 <code>List.zip</code>, which raises when the lengths differ.


## Signature

<div class="fsdocs-usage">
<code><span>Refined.NonEmptyList.zip&#32;<span>first&#32;second</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `first` | <code><span><a href="types/t-refined-nonemptylist.md">NonEmptyList</a>&lt;'first&gt;</span></code> |  |
| `second` | <code><span><a href="types/t-refined-nonemptylist.md">NonEmptyList</a>&lt;'second&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="types/t-refined-nonemptylist.md">NonEmptyList</a>&lt;<span>'first&#32;*&#32;'second</span>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/NonEmpty.fs#L219-219)
