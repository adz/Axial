---
title: "Schema.UnionDescription"
linkTitle: "UnionDescription"
weight: 1204
---

Describes a tagged union value schema.

## Signature

<div class="fsdocs-usage">
<code>type UnionDescription</code>
</div>

## Record Fields

| Field | Description |
| --- | --- |
| `DiscriminatorField` | The raw input field name that carries the case tag. |
| `PayloadField` | The raw input field name that carries the case payload. |
| `Cases` | The union cases in declaration order. |
