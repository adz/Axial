---
title: "Check.any"
linkTitle: "any"
weight: 2304
---

Returns success when at least one check in the sequence succeeds.

## Signature

<div class="fsdocs-usage">
<code><span>Check.any&#32;<span>checks</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `checks` | <code><span><span><a href="t-check.md">Check</a>&lt;'value&gt;</span>&#32;seq</span></code> | The checks to evaluate. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | A unit-success check that short-circuits on the first success. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span> <span class="id">Check</span><span class="pn">.</span><span class="id">isTrue</span> <span class="k">false</span><span class="pn">;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">isTrue</span> <span class="k">true</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">any</span> <span class="c">// Ok ()</span>
 <span class="pn">[</span> <span class="id">Check</span><span class="pn">.</span><span class="id">isTrue</span> <span class="k">false</span><span class="pn">;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">isTrue</span> <span class="k">false</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">any</span> <span class="c">// Error ()</span>
</code></pre>
