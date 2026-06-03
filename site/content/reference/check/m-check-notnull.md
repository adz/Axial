---
title: "Check.notNull"
linkTitle: "notNull"
weight: 2254
type: docs
---

Returns the value when it is not null.

## Signature

<div class="fsdocs-usage">
<code><span>Check.notNull&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'a</code> | The value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;'a&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> containing the non-null value; otherwise, an Error with unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="s">&quot;hello&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">notNull</span> <span class="c">// Ok &quot;hello&quot;</span>
 <span class="k">null</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">notNull</span> <span class="c">// Error ()</span>
</code></pre>
