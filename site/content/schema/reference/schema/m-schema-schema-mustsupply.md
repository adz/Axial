---
title: "Schema.mustSupply"
linkTitle: "mustSupply"
weight: 2110
type: docs
---

Requires boundary input for this schema to be supplied.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Schema.mustSupply&#32;<span>schema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="t-schema-schema.md">Schema</a>&lt;'a&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schema-schema.md">Schema</a>&lt;'a&gt;</span></code> |  |

## Remarks


 Supply is decided before a typed value exists, so it is Schema&#39;s concern rather than a value constraint.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Schema</span><span class="pn">.</span><span class="id">text</span> <span class="o">|&gt;</span> <span class="id">Schema</span><span class="pn">.</span><span class="id">mustSupply</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/SchemaApi.fs#L86-86)
