---
title: "Validation.traverseIndexed"
linkTitle: "traverseIndexed"
weight: 2117
---

Maps a sequence into validations while prefixing each item with its index.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.traverseIndexed&#32;<span>binder&#32;values</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `binder` | <code><span>int&#32;->&#32;'source&#32;->&#32;<span><a href="t-validation.md">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></span></code> | A function of type <code>int -&gt; &#39;source -&gt; Validation&lt;&#39;value, &#39;error&gt;</code>. |
| `values` | <code><span>'source&#32;seq</span></code> | The input sequence. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-validation.md">Validation</a>&lt;<span><span>'value&#32;list</span>,&#32;'error</span>&gt;</span></code> | A validation containing the list of values or accumulated diagnostics. |

## Remarks


 This is the indexed version of <a href="https://learn.microsoft.com/dotnet/api/sequence">sequence</a>. It is useful for list and array
 validation because each item can keep its own <a href="https://learn.microsoft.com/dotnet/api/fsflow.pathsegment.index">Index</a>
 branch without the caller manually wrapping every item.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="pn">[</span> <span class="s">&quot;a&quot;</span><span class="pn">;</span> <span class="s">&quot;b&quot;</span> <span class="pn">]</span> <span class="o">|&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">traverseIndexed</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">i</span> <span class="id">s</span> <span class="k">-&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="pn">(</span><span class="id">s</span><span class="pn">.</span><span class="id">ToUpper</span><span class="pn">(</span><span class="pn">)</span><span class="pn">)</span><span class="pn">)</span>
</code></pre>
