---
title: "Check.``or``"
linkTitle: "``or``"
weight: 2206
type: docs
---

Returns success when either check succeeds.

## Signature

<div class="fsdocs-usage">
<code><span>Check.``or``&#32;<span>left&#32;right</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `left` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;'left&gt;</span></code> | The first check. |
| `right` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;'right&gt;</span></code> | The second check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;unit&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> that succeeds if either input succeeds. |

## Remarks


 This is a logical &quot;or&quot; operation. It short-circuits: if <span class="fsdocs-param-name">left</span> succeeds,
 <span class="fsdocs-param-name">right</span> is not evaluated.
 

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Check</span><span class="pn">.</span><span class="k">or</span> <span class="pn">(</span><span class="id">Check</span><span class="pn">.</span><span class="id">okIf</span> <span class="k">true</span><span class="pn">)</span> <span class="pn">(</span><span class="id">Check</span><span class="pn">.</span><span class="id">okIf</span> <span class="k">false</span><span class="pn">)</span> <span class="c">// Ok ()</span>
 <span class="id">Check</span><span class="pn">.</span><span class="k">or</span> <span class="pn">(</span><span class="id">Check</span><span class="pn">.</span><span class="id">okIf</span> <span class="k">false</span><span class="pn">)</span> <span class="pn">(</span><span class="id">Check</span><span class="pn">.</span><span class="id">okIf</span> <span class="k">false</span><span class="pn">)</span> <span class="c">// Error ()</span>
</code></pre>



