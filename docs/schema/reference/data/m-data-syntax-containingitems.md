---
title: "Data.Syntax.containingItems"
linkTitle: "containingItems"
weight: 2514
---

Matches expected items as an unordered consumed subset.

## Signature

<div class="fsdocs-usage">
<code><span>Data.Syntax.containingItems&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>^a&#32;list</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-datapattern.md">DataPattern</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Data</span><span class="pn">.</span><span class="id">tryMatch</span> <span class="pn">[</span> <span class="id">at</span> <span class="s">&quot;items&quot;</span> <span class="pn">(</span><span class="id">containingItems</span> <span class="pn">[</span> <span class="s">&quot;Ada&quot;</span><span class="pn">;</span> <span class="s">&quot;Grace&quot;</span> <span class="pn">]</span><span class="pn">)</span> <span class="pn">]</span> <span class="id">actual</span>
 <span class="c">// Ok () when both values occur, in either order</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">namespace Microsoft.FSharp.Data</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L780-780)
