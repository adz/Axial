---
title: "Schema.JsonSchema.generate"
linkTitle: "generate"
weight: 2400
type: docs
---

Generates a compact JSON Schema document from any completed schema declaration.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.JsonSchema.generate&#32;<span>schema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="t-schema-schema.md">Schema</a>&lt;'model&gt;</span></code> | The record, primitive, collection, union, or other completed schema to lower. |

## Returns

| Type | Description |
| --- | --- |
| <code>string</code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">document</span> <span class="o">=</span> <span class="id">JsonSchema</span><span class="pn">.</span><span class="id">generate</span> <span class="id">customerSchema</span>
 <span class="c">// {&quot;$schema&quot;:&quot;https://json-schema.org/draft/2020-12/schema&quot;,&quot;type&quot;:&quot;object&quot;,&quot;properties&quot;:{...},&quot;required&quot;:[...]}</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val document: obj</div>
