---
title: "Check.nullOrEmpty"
linkTitle: "nullOrEmpty"
weight: 2413
type: docs
---

Returns success when the string is null or empty.

## Signature

<div class="fsdocs-usage">
<code><span>Check.nullOrEmpty&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>string</code> | The string to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-check.md">Check</a>&lt;unit&gt;</span></code> | <code>Ok ()</code> for null or empty strings; otherwise <code>Error ()</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="s">&quot;&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">nullOrEmpty</span> <span class="c">// Ok ()</span>
 <span class="s">&quot;hello&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">nullOrEmpty</span> <span class="c">// Error ()</span>
</code></pre>
