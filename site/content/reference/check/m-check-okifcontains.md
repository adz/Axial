---
title: "Check.okIfContains"
linkTitle: "okIfContains"
weight: 2237
type: docs
---

Returns success when the sequence contains the expected value.

## Signature

<div class="fsdocs-usage">
<code><span>Check.okIfContains&#32;<span>expected&#32;coll</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `expected` | <code>'value</code> | The value to search for. |
| `coll` | <code><span>'value&#32;seq</span></code> | The sequence to check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;unit&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> that succeeds when the value is present; otherwise, an Error with unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span> <span class="n">1</span><span class="pn">;</span> <span class="n">2</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfContains</span> <span class="n">2</span> <span class="c">// Ok ()</span>
 <span class="pn">[</span> <span class="n">1</span><span class="pn">;</span> <span class="n">2</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfContains</span> <span class="n">3</span> <span class="c">// Error ()</span>
</code></pre>
