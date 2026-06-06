---
title: "Check.negate"
linkTitle: "negate"
weight: 2300
---

Returns success when the supplied check fails.

## Signature

<div class="fsdocs-usage">
<code><span>Check.negate&#32;<span>check</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `check` | <code><span><a href="t-check.md">Check</a>&lt;'value&gt;</span></code> | The source check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | A unit-success check with inverted success/failure. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Check</span><span class="pn">.</span><span class="id">isTrue</span> <span class="k">true</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">negate</span> <span class="c">// Error ()</span>
 <span class="id">Check</span><span class="pn">.</span><span class="id">isTrue</span> <span class="k">false</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">negate</span> <span class="c">// Ok ()</span>
</code></pre>
