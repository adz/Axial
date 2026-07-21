---
title: "Schema.FieldDescription"
linkTitle: "FieldDescription"
weight: 1202
---

Describes one field of a model schema for inspection interpreters.

## Signature

<div class="fsdocs-usage">
<code>type FieldDescription</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `Name` | The boundary-facing external field name. |
| `Order` | The zero-based field order used for trusted construction and ordered interpreter output. |
| `Schema` | The description of the field's value schema. |
| `Constraints` | The portable constraint metadata attached at the field level, in declaration order. |
