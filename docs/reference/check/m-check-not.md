---
title: "Check.``not``"
linkTitle: "``not``"
---

Returns success when the supplied check fails.

## Signature

<div class="fsdocs-usage">
<code><span>Check.``not``&#32;<span>check</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `check` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;'value&gt;</span></code> | The source <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> to invert. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;unit&gt;</span></code> | A <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a> that succeeds if the input fails; otherwise, an Error with unit. |

## Remarks


 This is a logical &quot;not&quot; operation for checks. Note that it discards the success value
 and returns <a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-unit">Unit</a> on success.
 

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Check</span><span class="pn">.</span><span class="id">okIf</span> <span class="k">true</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">not</span> <span class="c">// Error ()</span>
 <span class="id">Check</span><span class="pn">.</span><span class="id">okIf</span> <span class="k">false</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">not</span> <span class="c">// Ok ()</span>
</code></pre>



