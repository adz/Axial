---
title: "Schema.JsonSchema.generate"
linkTitle: "generate"
weight: 2300
type: docs
---

Generates a compact JSON Schema document from a built model schema&#39;s metadata.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.JsonSchema.generate&#32;<span>schema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `schema` | <code><span><a href="t-schema-schema.md">Schema</a>&lt;'model&gt;</span></code> | The built model schema to lower. |

## Returns

| Type | Description |
| --- | --- |
| <code>string</code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">document</span> <span class="o">=</span> <span class="id">JsonSchema</span><span class="pn">.</span><span class="id">generate</span> <span class="id">customerSchema</span>
 <span class="c">// {&quot;type&quot;:&quot;object&quot;,&quot;properties&quot;:{...},&quot;required&quot;:[...]}</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val document: obj</div>
