---
title: "Schema.mayOmit"
linkTitle: "mayOmit"
weight: 2111
---

Allows boundary input for an option-typed schema to be omitted.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Schema.mayOmit&#32;<span>schema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="t-schema-schema.md">Schema</a>&lt;<span>'a&#32;option</span>&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schema-schema.md">Schema</a>&lt;<span>'a&#32;option</span>&gt;</span></code> |  |

## Remarks


 Only an option-typed schema can be omittable: any other type has nowhere to put an absent input, so the
 constructor could not be applied.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Schema</span><span class="pn">.</span><span class="id">option</span> <span class="id">Schema</span><span class="pn">.</span><span class="id">text</span> <span class="o">|&gt;</span> <span class="id">Schema</span><span class="pn">.</span><span class="id">mayOmit</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/SchemaApi.fs#L94-94)
