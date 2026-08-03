---
title: "Constraint.Renderer.Advanced.ofResolver"
linkTitle: "ofResolver"
weight: 2917
---

A renderer backed by a resolver that answers one contextual level at a time.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Renderer.Advanced.ofResolver&#32;<span>resolver</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `resolver` | <code><a href="t-constraint-messageresolver.md">MessageResolver</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Remarks


 Use this for ICU or any system that selects plural categories and renders entries itself. Axial still
 owns contextual fallback and violation composition; a system that must reorder a whole group takes
 <code>Violation.toMessageTree</code> instead.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Renderer</span><span class="pn">.</span><span class="id">Advanced</span><span class="pn">.</span><span class="id">ofResolver</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">request</span> <span class="k">-&gt;</span> <span class="id">icu</span><span class="pn">.</span><span class="id">TryRender</span><span class="pn">(</span><span class="id">request</span><span class="pn">.</span><span class="id">BaseKey</span><span class="pn">,</span> <span class="id">request</span><span class="pn">.</span><span class="id">Arguments</span><span class="pn">)</span><span class="pn">)</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L688-688)
