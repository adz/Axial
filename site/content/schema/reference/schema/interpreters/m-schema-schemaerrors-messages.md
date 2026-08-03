---
title: "Schema.SchemaErrors.messages"
linkTitle: "messages"
weight: 2214
type: docs
---

Renders each failure as a localized predicate, paired with the path it occurred at.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.SchemaErrors.messages&#32;<span>renderer&#32;errors</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `renderer` | <code><a href="../../../../error-handling/reference/constraint/t-constraint-renderer.md">Renderer</a></code> |  |
| `errors` | <code><a href="t-schema-schemaerrors.md">SchemaErrors</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><span>(<span><a href="t-schema-path.md">Path</a>&#32;*&#32;string</span>)</span>&#32;list</span></code> |  |

## Remarks


 Predicates, not sentences: the returned <code>Path</code> already identifies the field, so a form that renders
 its own label would otherwise print the field name twice. Supply only the document context — Schema folds
 its typed path in as the attribute itself.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">errors</span> <span class="o">|&gt;</span> <span class="id">SchemaErrors</span><span class="pn">.</span><span class="id">messages</span> <span class="pn">(</span><span class="id">renderer</span> <span class="o">|&gt;</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">context</span> <span class="s">&quot;signup&quot;</span><span class="pn">)</span>
 <span class="c">// [ Path &quot;name&quot;, &quot;must be present&quot; ]</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/SchemaErrors.fs#L157-157)
