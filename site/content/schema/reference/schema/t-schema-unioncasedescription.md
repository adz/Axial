---
title: "Schema.UnionCaseDescription"
linkTitle: "UnionCaseDescription"
weight: 1305
---

Describes one case in a tagged union value schema.

## Signature

<div class="fsdocs-usage">
<code>type UnionCaseDescription</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `Tag` | The raw discriminator tag for this union case. |
| `Payload` | The schema description of this case's payload. |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/Inspection.fs#L79-79)
