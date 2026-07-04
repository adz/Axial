---
title: "Flow.Ref.modify"
linkTitle: "modify"
weight: 2104
---

Updates the value of the reference using the supplied function and returns a derived value.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Ref.modify&#32;<span>f&#32;reference</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `f` | <code><span>'T&#32;->&#32;<span>'T&#32;*&#32;'v</span></span></code> | The update function of type <code>&#39;T -&gt; &#39;T * &#39;v</code>. |
| `reference` | <code><span><a href="t-flow-ref.md">Ref</a>&lt;'T&gt;</span></code> | The <a href="https://learn.microsoft.com/dotnet/api/axial.ref-1">Ref</a> to update. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../flow/t-flow-flow.md">Flow</a>&lt;<span>'env,&#32;'none,&#32;'v</span>&gt;</span></code> | A flow that updates the value and returns the second part of the tuple returned by <span class="fsdocs-param-name">f</span>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Ref</span><span class="pn">.</span><span class="id">modify</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">x</span> <span class="k">-&gt;</span> <span class="id">x</span> <span class="o">+</span> <span class="n">1</span><span class="pn">,</span> <span class="s">&quot;increased&quot;</span><span class="pn">)</span> <span class="id">myRef</span>
</code></pre>
