---
title: "Ref.update"
linkTitle: "update"
weight: 2103
---

Updates the value of the reference using the supplied function.

## Signature

<div class="fsdocs-usage">
<code><span>Ref.update&#32;<span>f&#32;reference</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `f` | <code><span>'T&#32;->&#32;'T</span></code> | The update function of type <code>&#39;T -&gt; &#39;T</code>. |
| `reference` | <code><span><a href="t-ref.md">Ref</a>&lt;'T&gt;</span></code> | The <a href="t-ref.md">Ref</a> to update. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="../flow/t-flow.md">Flow</a>&lt;<span>'env,&#32;'none,&#32;unit</span>&gt;</span></code> | A flow that updates the value and returns unit. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="id">Ref</span><span class="pn">.</span><span class="id">update</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">x</span> <span class="k">-&gt;</span> <span class="id">x</span> <span class="o">+</span> <span class="n">1</span><span class="pn">)</span> <span class="id">myRef</span>
</code></pre>
