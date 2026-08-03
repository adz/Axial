---
title: "Constraint.Catalogue.english"
linkTitle: "english"
weight: 3002
type: docs
---

The neutral English template for each entry, used when no resource resolves.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Catalogue.english&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-fsharpmap-2">Map</a>&lt;<span>string,&#32;string</span>&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Catalogue</span><span class="pn">.</span><span class="id">english</span><span class="pn">.</span><span class="pn">[</span><span class="s">&quot;constraint.presence.present&quot;</span><span class="pn">]</span> <span class="c">// &quot;must be present&quot;</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Catalogue.fs#L141-141)
