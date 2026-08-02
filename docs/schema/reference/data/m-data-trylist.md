---
title: "Data.tryList"
linkTitle: "tryList"
weight: 2705
---

Attempts to extract items from one list value.

## Signature

<div class="fsdocs-usage">
<code><span>Data.tryList&#32;<span>_arg1</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `_arg1` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><span><a href="t-data.md">Data</a>&#32;list</span>&#32;option</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Data</span><span class="pn">.</span><span class="id">tryList</span> <span class="pn">(</span><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="2" class="id">Data</span><span class="pn">.</span><span class="id">List</span> <span class="pn">[</span><span class="pn">]</span><span class="pn">)</span> <span class="c">// Some []</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">namespace Microsoft.FSharp.Data</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L664-664)
