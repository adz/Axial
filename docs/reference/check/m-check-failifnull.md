---
title: "Check.failIfNull"
linkTitle: "failIfNull"
weight: 2227
---

Returns the value when it is null.

## Signature

<div class="fsdocs-usage">
<code><span>Check.failIfNull&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'a</code> | The value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;'a&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> containing the null value; otherwise, an Error with unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">null</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfNull</span> <span class="c">// Ok null</span>
 <span class="s">&quot;hello&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfNull</span> <span class="c">// Error ()</span>
</code></pre>
