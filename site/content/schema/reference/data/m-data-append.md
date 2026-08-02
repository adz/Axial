---
title: "Data.append"
linkTitle: "append"
weight: 2316
type: docs
---

Appends one item to an existing list and returns the changed tree.

## Signature

<div class="fsdocs-usage">
<code><span>Data.append&#32;<span>path&#32;value&#32;input</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `path` | <code>string</code> |  |
| `value` | <code>^a</code> |  |
| `input` | <code><a href="t-data.md">Data</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-data.md">Data</a></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">data</span> <span class="pn">[</span> <span class="s">&quot;roles&quot;</span> <span class="o">=&gt;</span> <span class="pn">[</span> <span class="s">&quot;author&quot;</span> <span class="pn">]</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Data</span><span class="pn">.</span><span class="id">append</span> <span class="s">&quot;roles&quot;</span> <span class="s">&quot;admin&quot;</span>
 <span class="c">// data [ &quot;roles&quot; =&gt; [ &quot;author&quot;; &quot;admin&quot; ] ]</span>
</code></pre>





[Source](https://github.com/adz/Axial/blob/main/src/Axial.Data/DataErgonomics.fs#L164-164)
