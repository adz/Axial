---
title: "Constraint.MessageDescriptor.segments"
linkTitle: "segments"
weight: 2808
---

The parsed, unencoded key segments.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.MessageDescriptor.segments&#32;<span>descriptor</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `descriptor` | <code><a href="t-constraint-messagedescriptor.md">MessageDescriptor</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span>string&#32;list</span></code> |  |

## Remarks


 Segments exist for safe encoding and canonical reconstruction, not namespace fallback. Lookup for
 <code>books.isbn.invalid</code> never tries <code>books.isbn</code>.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/MessageKey.fs#L136-136)
