---
title: "Constraint.Catalogue.pluralArgument"
linkTitle: "pluralArgument"
weight: 3003
---

The argument each entry may be pluralized on, when it declares one.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Catalogue.pluralArgument&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-fsharpmap-2">Map</a>&lt;<span>string,&#32;<span>string&#32;option</span></span>&gt;</span></code> |  |

## Remarks


 At most one per entry. A translation may supply <code>&lt;key&gt;.one</code> and <code>&lt;key&gt;.other</code> for
 these; every other entry takes a single form.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Catalogue</span><span class="pn">.</span><span class="id">pluralArgument</span><span class="pn">.</span><span class="pn">[</span><span class="s">&quot;constraint.cardinality.minimum&quot;</span><span class="pn">]</span> <span class="c">// Some &quot;minimum&quot;</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Catalogue.fs#L152-152)
