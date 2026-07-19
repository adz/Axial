---
title: "Schema.UnionDescription"
linkTitle: "UnionDescription"
weight: 1204
type: docs
---

Describes a tagged union value schema.

## Signature

<div class="fsdocs-usage">
<code>type UnionDescription</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `DiscriminatorField` | The structured data field name that carries the case tag. |
| `PayloadField` | The structured data field name that carries the case payload. |
| `Cases` | The union cases in declaration order. |
