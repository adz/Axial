---
title: "Check.``and``"
linkTitle: "``and``"
weight: 2205
---

Returns success when both checks succeed.

## Signature

<div class="fsdocs-usage">
<code><span>Check.``and``&#32;<span>left&#32;right</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `left` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;'left&gt;</span></code> | The first check. |
| `right` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;'right&gt;</span></code> | The second check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;unit&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> that succeeds only if both inputs succeed. |

## Remarks


 This is a logical &quot;and&quot; operation. It short-circuits: if <span class="fsdocs-param-name">left</span> fails,
 <span class="fsdocs-param-name">right</span> is not evaluated.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Check</span><span class="pn">.</span><span class="k">and</span> <span class="pn">(</span><span class="id">Check</span><span class="pn">.</span><span class="id">okIf</span> <span class="k">true</span><span class="pn">)</span> <span class="pn">(</span><span class="id">Check</span><span class="pn">.</span><span class="id">okIf</span> <span class="k">true</span><span class="pn">)</span> <span class="c">// Ok ()</span>
 <span class="id">Check</span><span class="pn">.</span><span class="k">and</span> <span class="pn">(</span><span class="id">Check</span><span class="pn">.</span><span class="id">okIf</span> <span class="k">true</span><span class="pn">)</span> <span class="pn">(</span><span class="id">Check</span><span class="pn">.</span><span class="id">okIf</span> <span class="k">false</span><span class="pn">)</span> <span class="c">// Error ()</span>
</code></pre>
