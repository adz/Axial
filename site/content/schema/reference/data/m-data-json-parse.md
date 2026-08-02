---
title: "Data.Json.parse"
linkTitle: "parse"
weight: 2600
type: docs
---

Parses one JSON value into structured data.

## Signature

<div class="fsdocs-usage">
<code><span>Data.Json.parse&#32;<span>text</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `text` | <code>string</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-data.md">Data</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Data</span><span class="pn">.</span><span class="id">Json</span><span class="pn">.</span><span class="id">parse</span> <span class="s">&quot;{\&quot;name\&quot;:\&quot;Ada\&quot;}&quot;</span>
 <span class="c">// Data.Object [ &quot;name&quot;, Data.Text &quot;Ada&quot; ]</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">namespace Microsoft.FSharp.Data</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L833-833)
