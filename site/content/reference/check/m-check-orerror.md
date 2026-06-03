---
title: "Check.orError"
linkTitle: "orError"
weight: 2252
type: docs
---

Maps a unit error into the supplied application error value.

## Signature

<div class="fsdocs-usage">
<code><span>Check.orError&#32;<span>error&#32;result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `error` | <code>'error</code> | The domain error of type <code>&#39;error</code> to return on failure. |
| `result` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>&lt;'value&gt;</span></code> | The source <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-check-1.html">Check</a>. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | A <a href="https://learn.microsoft.com/dotnet/api/system.result-2">Result</a> with the provided error value. |

## Remarks


 This is the primary bridge from checks to domain-specific results.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="s">&quot;&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfNonEmptyStr</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">orError</span> <span class="s">&quot;Empty string&quot;</span> <span class="c">// Error &quot;Empty string&quot;</span>
 <span class="s">&quot;hello&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">okIfNonEmptyStr</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">orError</span> <span class="s">&quot;Empty string&quot;</span> <span class="c">// Ok &quot;hello&quot;</span>
</code></pre>
