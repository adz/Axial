---
title: "Check.atMostOne"
linkTitle: "atMostOne"
weight: 2421
---

Returns success when the sequence contains at most one item.

## Signature

<div class="fsdocs-usage">
<code><span>Check.atMostOne&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> when zero or one item is present; otherwise <code>Error ()</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span><span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">atMostOne</span> <span class="c">// Ok ()</span>
 <span class="pn">[</span> <span class="n">1</span><span class="pn">;</span> <span class="n">2</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">atMostOne</span> <span class="c">// Error ()</span>
</code></pre>
