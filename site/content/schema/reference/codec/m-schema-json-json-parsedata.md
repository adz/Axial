---
title: "Schema.Json.parseData"
linkTitle: "parseData"
weight: 2100
type: docs
---

Parses one JSON value into source-neutral structured data.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Json.Json.parseData&#32;<span>json</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `json` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="../../../data/reference/data/t-data.md">Data</a></code> |  |

## Remarks


 Preserves object field order, duplicate field names, and the original spelling of number tokens. This parser is
 available on .NET and Fable. It does not apply a model schema; use <code>deserialize</code> with a compiled codec when
 decoding directly to a schema-described model.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Json</span><span class="pn">.</span><span class="id">parseData</span> <span class="s">&quot;{\&quot;name\&quot;:\&quot;Ada\&quot;}&quot;</span>
 <span class="c">// Data.Object [ &quot;name&quot;, Data.Text &quot;Ada&quot; ]</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema.Json/Json.fs#L1164-1164)
