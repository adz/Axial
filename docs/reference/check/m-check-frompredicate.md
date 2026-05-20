---
title: "Check.fromPredicate"
linkTitle: "fromPredicate"
weight: 2200
---

Builds a check from a predicate while preserving the successful value.

## Signature

<div class="fsdocs-usage">
<code><span>Check.fromPredicate&#32;<span>predicate&#32;value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `predicate` | <code><span>'value&#32;->&#32;bool</span></code> | A function of type <code>&#39;value -&gt; bool</code> to test the value. |
| `value` | <code>'value</code> | The value of type <code>&#39;value</code> to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;'value&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> containing the value if the predicate succeeds; otherwise, an Error with unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="n">5</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">fromPredicate</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">x</span> <span class="k">-&gt;</span> <span class="id">x</span> <span class="pn">&gt;</span> <span class="n">0</span><span class="pn">)</span> <span class="c">// Ok 5</span>
 <span class="o">-</span><span class="n">1</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">fromPredicate</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">x</span> <span class="k">-&gt;</span> <span class="id">x</span> <span class="pn">&gt;</span> <span class="n">0</span><span class="pn">)</span> <span class="c">// Error ()</span>
</code></pre>



