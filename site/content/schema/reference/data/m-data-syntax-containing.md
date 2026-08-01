---
title: "Data.Syntax.containing"
linkTitle: "containing"
weight: 2513
type: docs
---

Creates a partial object pattern from required fields.

## Signature

<div class="fsdocs-usage">
<code><span>Data.Syntax.containing&#32;<span>fields</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `fields` | <code><span><a href="t-datafield.md">DataField</a>&#32;list</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-datapattern.md">DataPattern</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Data</span><span class="pn">.</span><span class="id">tryMatch</span> <span class="pn">[</span> <span class="id">at</span> <span class="s">&quot;&quot;</span> <span class="pn">(</span><span class="id">containing</span> <span class="pn">[</span> <span class="s">&quot;id&quot;</span> <span class="o">=&gt;</span> <span class="n">42</span> <span class="pn">]</span><span class="pn">)</span> <span class="pn">]</span> <span class="pn">(</span><span class="id">data</span> <span class="pn">[</span> <span class="s">&quot;id&quot;</span> <span class="o">=&gt;</span> <span class="n">42</span><span class="pn">;</span> <span class="s">&quot;extra&quot;</span> <span class="o">=&gt;</span> <span class="k">true</span> <span class="pn">]</span><span class="pn">)</span>
 <span class="c">// Ok ()</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">namespace Microsoft.FSharp.Data</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L696-696)
