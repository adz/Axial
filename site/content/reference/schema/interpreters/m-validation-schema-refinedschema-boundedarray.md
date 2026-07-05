---
title: "Validation.Schema.RefinedSchema.boundedArray"
linkTitle: "boundedArray"
weight: 2311
type: docs
---

Describes a bounded array as a schema refined value over a collection with inclusive count bounds.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Schema.RefinedSchema.boundedArray&#32;<span>minCount&#32;maxCount&#32;itemSchema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `minCount` | <code>int</code> |  |
| `maxCount` | <code>int</code> |  |
| `itemSchema` | <code><span><a href="../t-schema-valueschema.md">ValueSchema</a>&lt;'value&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../t-schema-valueschema.md">ValueSchema</a>&lt;<span><a href="../../refined/t-refined-boundedarray.md">BoundedArray</a>&lt;'value&gt;</span>&gt;</span></code> |  |
