---
title: "Check.all"
linkTitle: "all"
weight: 2207
---

Returns success when every check in the sequence succeeds.

## Signature

<div class="fsdocs-usage">
<code><span>Check.all&#32;<span>checks</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `checks` | <code><span><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;'value&gt;</span>&#32;seq</span></code> | A sequence of checks. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;unit&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> that succeeds only if all inputs succeed. |

## Remarks


 Sequentially evaluates each check in the <span class="fsdocs-param-name">checks</span> sequence.
 Stops at the first failure.
 

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIf</span> <span class="k">true</span><span class="pn">;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIf</span> <span class="k">true</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">all</span> <span class="c">// Ok ()</span>
 <span class="pn">[</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIf</span> <span class="k">true</span><span class="pn">;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIf</span> <span class="k">false</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">all</span> <span class="c">// Error ()</span>
</code></pre>



