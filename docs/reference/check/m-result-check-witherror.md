---
title: "Result.Check.withError"
linkTitle: "withError"
weight: 3100
---

Assigns the supplied application error to a unit-error check failure.

## Signature

<div class="fsdocs-usage">
<code><span>Result.Check.withError&#32;<span>error&#32;result</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `error` | <code>'error</code> | The domain error to return when the check fails. |
| `result` | <code><span><a href="/reference/Axial/axial-result-check-1.html">Check</a>&lt;'value&gt;</span></code> | The source unit-error check. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The successful check value, or the supplied error when the check fails. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="s">&quot;&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">notBlank</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">withError</span> <span class="s">&quot;Name required&quot;</span> <span class="c">// Error &quot;Name required&quot;</span>
 <span class="s">&quot;Ada&quot;</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">notBlank</span> <span class="o">|&gt;</span> <span class="id">Check</span><span class="pn">.</span><span class="id">withError</span> <span class="s">&quot;Name required&quot;</span> <span class="c">// Ok ()</span>
</code></pre>
