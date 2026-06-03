---
title: "Validation.bind"
linkTitle: "bind"
weight: 2107
type: docs
---

Sequences a validation-producing continuation.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.bind&#32;<span>binder&#32;validation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `binder` | <code><span>'value&#32;->&#32;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-validation-2.html">Validation</a>&lt;<span>'next,&#32;'error</span>&gt;</span></span></code> | A function of type <code>&#39;value -&gt; Validation&lt;&#39;next, &#39;error&gt;</code>. |
| `validation` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-validation-2.html">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The source validation. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-validation-2.html">Validation</a>&lt;<span>'next,&#32;'error</span>&gt;</span></code> | The result of the binder or the original diagnostics. |

## Remarks


 This is the monadic &quot;bind&quot; for validation. Note that this operation short-circuits
 and does not accumulate errors from the binder if the source has already failed.
 For accumulation, use <a href="https://learn.microsoft.com/dotnet/api/map2">map2</a> or the applicative <code>and!</code> syntax.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="n">5</span> <span class="o">|&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">bind</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">x</span> <span class="k">-&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="pn">(</span><span class="id">x</span> <span class="o">+</span> <span class="n">1</span><span class="pn">)</span><span class="pn">)</span> <span class="c">// Validation (Ok 6)</span>
</code></pre>
