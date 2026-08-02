---
title: "Data.tryBool"
linkTitle: "tryBool"
weight: 2703
---

Attempts to extract a Boolean from one structured value.

## Signature

<div class="fsdocs-usage">
<code><span>Data.tryBool&#32;<span>_arg1</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `_arg1` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span>bool&#32;option</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Data</span><span class="pn">.</span><span class="id">tryBool</span> <span class="pn">(</span><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="2" class="id">Data</span><span class="pn">.</span><span class="id">Bool</span> <span class="k">true</span><span class="pn">)</span> <span class="c">// Some true</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">namespace Microsoft.FSharp.Data</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L229-229)
