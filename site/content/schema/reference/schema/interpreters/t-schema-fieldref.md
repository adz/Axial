---
title: "Schema.FieldRef"
linkTitle: "FieldRef<model, value>"
weight: 1403
type: docs
---

A typed, named reference to one field of a schema-described model.

## Signature

<div class="fsdocs-usage">
<code>type FieldRef<'model, 'value></code>
</div>

## Type Parameters

| Name |
| --- |
| `model` |
| `value` |

## Record Fields

| Field | Description |
| --- | --- |
| `Name` | The field's external (wire) name, as declared on the schema. |
| `Get` | Reads the field's value from a model. |
| `Set` | Returns a copy of the model with this field replaced. |

## Remarks

<p class='fsdocs-para'>
 A field reference pairs the field&#39;s external (wire) name with typed getter and immutable setter functions, so code that needs to talk about
 a field — contextual rules, redisplay, UI binding — can reference it as an ordinary value instead of re-typing
 the wire name as a string that can silently drift from the schema. Generated schema declarations emit one
 <code>FieldRef</code> per field; hand-written schemas can declare them alongside the schema.
 </p>
