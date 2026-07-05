---
title: "Schema.Value.manyOf"
linkTitle: "manyOf"
weight: 2106
type: docs
---

Describes a collection of values from an already built item value schema.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Value.manyOf&#32;<span>itemSchema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `itemSchema` | <code><span><a href="t-schema-valueschema.md">ValueSchema</a>&lt;'item&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schema-valueschema.md">ValueSchema</a>&lt;<span>'item&#32;list</span>&gt;</span></code> |  |

## Remarks

<p class='fsdocs-para'><code>manyOf</code> is the general collection constructor. Use it when each item is a primitive, refined/domain value,
 nested model value, or another collection value schema. Collection-level constraints such as <code>minCount</code>
 attach to the returned schema; item-level constraints stay on <span class="fsdocs-param-name">itemSchema</span> and interpreters
 attach their diagnostics to item index paths.
 </p>
