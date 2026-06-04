---
title: "Check.orErrorWith"
linkTitle: "orErrorWith"
weight: 2253
---

Maps a unit error into an application error produced on demand.

## Signature

<div class="fsdocs-usage">
<code><span>Check.orErrorWith&#32;<span>errorFn&#32;result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `errorFn` | <code><span>unit&#32;->&#32;'error</span></code> | A function of type <code>unit -&gt; &#39;error</code> to produce the error. |
| `result` | <code><span><a href="t-check.md">Check</a>&lt;'value&gt;</span></code> | The source <a href="t-check.md">Check</a>. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | A <a href="https://learn.microsoft.com/dotnet/api/system.result-2">Result</a> with the produced error value. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="s">&quot;&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfNonEmptyStr</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">orErrorWith</span> <span class="pn">(</span><span class="k">fun</span> <span class="pn">(</span><span class="pn">)</span> <span class="k">-&gt;</span> <span class="s">&quot;Empty string&quot;</span><span class="pn">)</span> <span class="c">// Error &quot;Empty string&quot;</span>
</code></pre>
