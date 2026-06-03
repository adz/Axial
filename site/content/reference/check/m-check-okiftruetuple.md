---
title: "Check.okIfTrueTuple"
linkTitle: "okIfTrueTuple"
weight: 2203
type: docs
---

Alias for <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-checkmodule.html">Check.fromTry</a> when tuple-form intent should be explicit.

## Signature

<div class="fsdocs-usage">
<code><span>Check.okIfTrueTuple&#32;<span><span>(<span>arg1,&#32;arg1</span>)</span></span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `arg0` | <code>bool</code> |  |
| `arg1` | <code>'value</code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;'value&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> containing the value when the flag is true; otherwise, an Error with unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">(</span><span class="k">true</span><span class="pn">,</span> <span class="s">&quot;value&quot;</span><span class="pn">)</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfTrueTuple</span> <span class="c">// Ok &quot;value&quot;</span>
 <span class="pn">(</span><span class="k">false</span><span class="pn">,</span> <span class="s">&quot;value&quot;</span><span class="pn">)</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfTrueTuple</span> <span class="c">// Error ()</span>
</code></pre>
