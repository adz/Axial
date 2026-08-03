---
title: "Constraint.MessageRequest"
linkTitle: "MessageRequest"
weight: 1902
type: docs
---

One contextual level&#39;s request to an advanced resolver.

## Signature

<div class="fsdocs-usage">
<code>type MessageRequest</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `BaseKey` | The encoded contextual resource key, without a plural suffix. |
| `Arguments` | The operands the entry may interpolate. |
| `PluralArgument` | The owning catalogue's plural operand, when it declares one. |

## Remarks

<code>BaseKey</code> is an encoded contextual resource key with no plural suffix applied. A resolver that selects
 plural categories itself reads <code>PluralArgument</code> and <code>Arguments</code> and answers for the whole level.



[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L21-21)
