---
title: "Schema.RefinedSchemas.bounded"
linkTitle: "bounded"
weight: 2308
---

 Builds a schema for a value confined to the supplied bounds. The bounds belong to
 the schema rather than to each value, so they are supplied once here.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.RefinedSchemas.bounded&#32;<span>bounds&#32;itemSchema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `bounds` | <code><span><a href="../../../../error-handling/reference/refined/types/t-refined-interval.md">Interval</a>&lt;'value&gt;</span></code> |  |
| `itemSchema` | <code><span><a href="../t-schema-schema.md">Schema</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-schema-schema.md">Schema</a>&lt;<span><a href="../../../../error-handling/reference/refined/types/t-refined-bounded.md">Bounded</a>&lt;'value&gt;</span>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/RefinedSchemas.fs#L47-47)
