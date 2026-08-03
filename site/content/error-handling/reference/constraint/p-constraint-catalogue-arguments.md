---
title: "Constraint.Catalogue.arguments"
linkTitle: "arguments"
weight: 3001
type: docs
---

The argument names each entry&#39;s template may interpolate.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Catalogue.arguments&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-fsharpmap-2">Map</a>&lt;<span>string,&#32;<span>string&#32;list</span></span>&gt;</span></code> |  |

## Remarks

<code>actual</code> is not listed: it never appears in a predicate. It reaches a message through the separate
 <code>constraint.actual</code> composition entry, which is what keeps it optional without an optional-placeholder
 rule.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Catalogue</span><span class="pn">.</span><span class="id">arguments</span><span class="pn">.</span><span class="pn">[</span><span class="s">&quot;constraint.cardinality.between&quot;</span><span class="pn">]</span> <span class="c">// [ &quot;minimum&quot;; &quot;maximum&quot; ]</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Catalogue.fs#L134-134)
