---
title: "Schema.ValueDescription"
linkTitle: "ValueDescription"
weight: 1101
---

Describes one value schema: its shape, declared format, and portable constraint metadata.

## Signature

<div class="fsdocs-usage">
<code>type ValueDescription</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `Shape` | The structural shape of the value. |
| `Format` | The declared boundary format, when one was attached with <code>Value.withFormat</code>. |
| `Constraints` | The portable constraint metadata attached to this value schema layer, in declaration order. |
