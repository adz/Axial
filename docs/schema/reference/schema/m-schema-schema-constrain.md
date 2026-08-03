---
title: "Schema.constrain"
linkTitle: "constrain"
weight: 2108
---

Requires a schema&#39;s values to satisfy a constraint.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Schema.constrain&#32;<span>constraint'&#32;schema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `constraint'` | <code><span><a href="../../../error-handling/reference/constraint/t-constraint-constraint.md">Constraint</a>&lt;'a&gt;</span></code> |  |
| `schema` | <code><span><a href="t-schema-schema.md">Schema</a>&lt;'a&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schema-schema.md">Schema</a>&lt;'a&gt;</span></code> |  |

## Remarks


 The same <code>Constraint</code> value serves direct checking, refinement, and Schema. For a value schema the
 constraint runs at that layer; for a model schema it runs after successful field admission and
 construction.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Schema</span><span class="pn">.</span><span class="id">text</span> <span class="o">|&gt;</span> <span class="id">Schema</span><span class="pn">.</span><span class="id">constrain</span> <span class="pn">(</span><span class="id">Constraint</span><span class="pn">.</span><span class="id">lengthBetween</span> <span class="n">2</span> <span class="n">40</span><span class="pn">)</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/SchemaApi.fs#L75-75)
