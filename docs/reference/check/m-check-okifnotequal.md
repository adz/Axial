---
title: "Check.okIfNotEqual"
linkTitle: "okIfNotEqual"
weight: 2239
---

Returns success when the values are not equal.

## Signature

<div class="fsdocs-usage">
<code><span>Check.okIfNotEqual&#32;<span>expected&#32;actual</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>'a</code> | The expected value. |
| `actual` | <code>'a</code> | The actual value. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | A <a href="t-check.md">Check</a> that succeeds if the values differ; otherwise, an Error with unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfNotEqual</span> <span class="n">5</span> <span class="n">6</span> <span class="c">// Ok ()</span>
 <span class="id">Check</span><span class="pn">.</span><span class="id">okIfNotEqual</span> <span class="n">5</span> <span class="n">5</span> <span class="c">// Error ()</span>
</code></pre>
