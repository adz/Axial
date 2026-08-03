---
title: "Constraint.MessageFormatSpec.pluralArgument"
linkTitle: "pluralArgument"
weight: 2814
---

The argument a translator may pluralize on, when the catalogue declares one.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.MessageFormatSpec.pluralArgument&#32;<span>spec</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `spec` | <code><a href="t-constraint-messageformatspec.md">MessageFormatSpec</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span>string&#32;option</span></code> |  |

## Remarks


 At most one per entry. Ordinary lookup supports <code>.one</code> for an operand exactly equal to one and
 <code>.other</code> otherwise; full CLDR selection belongs to an advanced resolver.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/MessageKey.fs#L195-195)
