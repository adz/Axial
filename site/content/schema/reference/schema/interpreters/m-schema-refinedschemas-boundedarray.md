---
title: "Schema.RefinedSchemas.boundedArray"
linkTitle: "boundedArray"
weight: 2313
type: docs
---

Describes a bounded array as a schema refined value over a collection with inclusive count bounds.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.RefinedSchemas.boundedArray&#32;<span>minCount&#32;maxCount&#32;itemSchema</span></span></code>
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
| <code><span><a href="../t-schema-schema.md">Schema</a>&lt;<span><a href="../../../../error-handling/reference/refined/t-refined-boundedarray.md">BoundedArray</a>&lt;'value&gt;</span>&gt;</span></code> |  |
