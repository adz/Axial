---
title: "Schema.RefinedSchemas.interval"
linkTitle: "interval"
weight: 2306
---


 Builds a schema for an inclusive range, replacing the former per-type range
 schemas. Generic over any ordered value, so one definition covers what
 <code>dateTimeOffsetRange</code> and <code>dateOnlyRange</code> each needed separately.


## Signature

<div class="fsdocs-usage">
<code><span>Schema.RefinedSchemas.interval&#32;<span>itemSchema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `itemSchema` | <code><span><a href="../t-schema-schema.md">Schema</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-schema-schema.md">Schema</a>&lt;<span><a href="../../../../values/reference/refined/types/t-refined-interval.md">Interval</a>&lt;'value&gt;</span>&gt;</span></code> |  |


[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/RefinedSchemas.fs#L36-36)
