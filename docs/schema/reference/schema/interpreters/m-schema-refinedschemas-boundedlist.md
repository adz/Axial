---
title: "Schema.RefinedSchemas.boundedList"
linkTitle: "boundedList"
weight: 2312
---

Describes a bounded list as a schema refined value over a collection with inclusive count bounds.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.RefinedSchemas.boundedList&#32;<span>minCount&#32;maxCount&#32;itemSchema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minCount` | <code>int</code> |  |
| `maxCount` | <code>int</code> |  |
| `itemSchema` | <code><span><a href="../t-schema-schema.md">Schema</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-schema-schema.md">Schema</a>&lt;<span><a href="../../../../error-handling/reference/refined/types/t-refined-boundedlist.md">BoundedList</a>&lt;'value&gt;</span>&gt;</span></code> |  |
