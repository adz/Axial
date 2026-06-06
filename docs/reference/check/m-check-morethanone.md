---
title: "Check.moreThanOne"
linkTitle: "moreThanOne"
weight: 2423
---

Returns success when the sequence contains more than one item.

## Signature

<div class="fsdocs-usage">
<code><span>Check.moreThanOne&#32;<span>values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `values` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> when more than one item is present; otherwise <code>Error ()</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span> <span class="n">1</span><span class="pn">;</span> <span class="n">2</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">moreThanOne</span> <span class="c">// Ok ()</span>
 <span class="pn">[</span> <span class="n">1</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">moreThanOne</span> <span class="c">// Error ()</span>
</code></pre>
