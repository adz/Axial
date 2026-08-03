---
title: "Schema.SchemaDescription"
linkTitle: "SchemaDescription"
weight: 1301
type: docs
---

Describes one value schema: its shape, declared format, and portable constraint metadata.

## Signature

<div class="fsdocs-usage">
<code>type SchemaDescription</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `Shape` | The structural shape of the value. |
| `Format` | The declared boundary format, when one was attached with <code>Schema.withFormat</code>. |
| `Constraints` | The constraint descriptions attached to this value schema layer, in declaration order. |
| `Supply` | The boundary supply declaration attached to this layer, when one was made. |
| `Description` | The description metadata, when one was attached with <code>Schema.describe</code>. |
| `Default` | The default-value metadata, when one was attached with <code>Schema.withDefault</code>. |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/Inspection.fs#L44-44)
