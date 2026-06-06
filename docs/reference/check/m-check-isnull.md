---
title: "Check.isNull"
linkTitle: "isNull"
weight: 2409
---

Returns success when the reference is null.

## Signature

<div class="fsdocs-usage">
<code><span>Check.isNull&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'value</code> | The reference value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for null values; otherwise <code>Error ()</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">null</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">isNull</span> <span class="c">// Ok ()</span>
 <span class="s">&quot;hello&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">isNull</span> <span class="c">// Error ()</span>
</code></pre>
