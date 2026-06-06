---
title: "Check.either"
linkTitle: "either"
weight: 2302
---

Returns success when either check succeeds.

## Signature

<div class="fsdocs-usage">
<code><span>Check.either&#32;<span>left&#32;right</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `left` | <code><span><a href="t-check.md">Check</a>&lt;'left&gt;</span></code> | The first check. |
| `right` | <code><span><a href="t-check.md">Check</a>&lt;'right&gt;</span></code> | The second check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | A unit-success check that short-circuits on the first success. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Check</span><span class="pn">.</span><span class="id">either</span> <span class="pn">(</span><span class="id">Check</span><span class="pn">.</span><span class="id">isTrue</span> <span class="k">false</span><span class="pn">)</span> <span class="pn">(</span><span class="id">Check</span><span class="pn">.</span><span class="id">some</span> <span class="pn">(</span><span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">Some</span> <span class="n">10</span><span class="pn">)</span><span class="pn">)</span> <span class="c">// Ok ()</span>
 <span class="id">Check</span><span class="pn">.</span><span class="id">either</span> <span class="pn">(</span><span class="id">Check</span><span class="pn">.</span><span class="id">isTrue</span> <span class="k">false</span><span class="pn">)</span> <span class="pn">(</span><span class="id">Check</span><span class="pn">.</span><span class="id">isTrue</span> <span class="k">false</span><span class="pn">)</span> <span class="c">// Error ()</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">union case Option.Some: Value: &#39;T -&gt; Option&lt;&#39;T&gt;</div>
