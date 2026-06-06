---
title: "Check.all"
linkTitle: "all"
weight: 2303
---

Returns success when every check in the sequence succeeds.

## Signature

<div class="fsdocs-usage">
<code><span>Check.all&#32;<span>checks</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `checks` | <code><span><span><a href="t-check.md">Check</a>&lt;'value&gt;</span>&#32;seq</span></code> | The checks to evaluate. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | A unit-success check that short-circuits on the first failure. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span> <span class="id">Check</span><span class="pn">.</span><span class="id">isTrue</span> <span class="k">true</span><span class="pn">;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">isTrue</span> <span class="k">true</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">all</span> <span class="c">// Ok ()</span>
 <span class="pn">[</span> <span class="id">Check</span><span class="pn">.</span><span class="id">isTrue</span> <span class="k">true</span><span class="pn">;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">isTrue</span> <span class="k">false</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">all</span> <span class="c">// Error ()</span>
</code></pre>
