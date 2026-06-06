---
title: "Take.whenNotNull"
linkTitle: "whenNotNull"
weight: 2006
---

Keeps the reference when it is not null.

## Signature

<div class="fsdocs-usage">
<code><span>Take.whenNotNull&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'value</code> | The reference value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../check/t-check.md">Check</a>&lt;'value&gt;</span></code> | <code>Ok value</code> when non-null; otherwise <code>Error ()</code>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="s">&quot;hello&quot;</span> <span class="o">|&gt;</span> <span class="id">Take</span><span class="pn">.</span><span class="id">whenNotNull</span> <span class="c">// Ok &quot;hello&quot;</span>
 <span class="k">null</span> <span class="o">|&gt;</span> <span class="id">Take</span><span class="pn">.</span><span class="id">whenNotNull</span> <span class="c">// Error ()</span>
</code></pre>
