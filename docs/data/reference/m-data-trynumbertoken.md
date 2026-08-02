---
title: "Data.tryNumberToken"
linkTitle: "tryNumberToken"
weight: 2704
---

Attempts to extract the preserved token from one number value.

## Signature

<div class="fsdocs-usage">
<code><span>Data.tryNumberToken&#32;<span>_arg1</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `_arg1` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span>string&#32;option</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Data</span><span class="pn">.</span><span class="id">tryNumberToken</span> <span class="pn">(</span><span class="id">Data</span><span class="pn">.</span><span class="id">Number</span> <span class="s">&quot;1e3&quot;</span><span class="pn">)</span> <span class="c">// Some &quot;1e3&quot;</span>
</code></pre>





[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L233-233)
