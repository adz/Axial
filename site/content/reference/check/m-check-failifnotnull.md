---
title: "Check.failIfNotNull"
linkTitle: "failIfNotNull"
weight: 2226
type: docs
---

Returns success when the value is null.

## Signature

<div class="fsdocs-usage">
<code><span>Check.failIfNotNull&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'a</code> | The value to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;unit&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> that succeeds if the value is null; otherwise, an Error with unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">null</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfNotNull</span> <span class="c">// Ok ()</span>
 <span class="s">&quot;hello&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">failIfNotNull</span> <span class="c">// Error ()</span>
</code></pre>
