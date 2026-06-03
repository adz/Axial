---
title: "Check.okIfNotEmpty"
linkTitle: "okIfNotEmpty"
weight: 2228
---

Returns the sequence when it is not empty.

## Signature

<div class="fsdocs-usage">
<code><span>Check.okIfNotEmpty&#32;<span>coll</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `coll` | <code><span>'a&#32;seq</span></code> | The sequence of type <code>seq&lt;&#39;a&gt;</code> to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;<span>'a&#32;seq</span>&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> containing the non-empty sequence; otherwise, an Error with unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span><span class="n">1</span><span class="pn">;</span> <span class="n">2</span><span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfNotEmpty</span> <span class="c">// Ok [1; 2]</span>
 <span class="pn">[</span><span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfNotEmpty</span> <span class="c">// Error ()</span>
</code></pre>
