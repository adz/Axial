---
title: "Schema.UnionCaseDescription"
linkTitle: "UnionCaseDescription"
weight: 1205
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
