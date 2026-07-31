---
title: "Refined.NonBlankString.split"
linkTitle: "split"
weight: 2804
type: docs
---

 Splits on a separator, discarding blank segments. Returns a non-empty list because
 inhabited text always yields at least one inhabited segment.

## Signature

<div class="fsdocs-usage">
<code><span>Refined.NonBlankString.split&#32;<span>separator&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `separator` | <code>string</code> |  |
| `input` | <code><a href="types/t-refined-nonblankstring.md">NonBlankString</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="types/t-refined-nonemptylist.md">NonEmptyList</a>&lt;<a href="types/t-refined-nonblankstring.md">NonBlankString</a>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Refined/Refine.fs#L86-86)
