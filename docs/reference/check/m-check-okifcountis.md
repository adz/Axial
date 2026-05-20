---
title: "Check.okIfCountIs"
linkTitle: "okIfCountIs"
weight: 2236
---

Returns success when the sequence count matches the expected count.

## Signature

<div class="fsdocs-usage">
<code><span>Check.okIfCountIs&#32;<span>expected&#32;coll</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>int</code> | The expected item count. |
| `coll` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;unit&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> that succeeds when the count matches; otherwise, an Error with unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span> <span class="n">1</span><span class="pn">;</span> <span class="n">2</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfCountIs</span> <span class="n">2</span> <span class="c">// Ok ()</span>
 <span class="pn">[</span> <span class="n">1</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfCountIs</span> <span class="n">2</span> <span class="c">// Error ()</span>
</code></pre>
