---
title: "Schema.constrainAll"
linkTitle: "constrainAll"
weight: 2109
type: docs
---

Requires a schema&#39;s values to satisfy every constraint, in declaration order.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.Schema.constrainAll&#32;<span>constraints&#32;schema</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `constraints` | <code><span><span><a href="../../../error-handling/reference/constraint/t-constraint-constraint.md">Constraint</a>&lt;'a&gt;</span>&#32;list</span></code> |  |
| `schema` | <code><span><a href="t-schema-schema.md">Schema</a>&lt;'a&gt;</span></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-schema-schema.md">Schema</a>&lt;'a&gt;</span></code> |  |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Schema</span><span class="pn">.</span><span class="id">text</span> <span class="o">|&gt;</span> <span class="id">Schema</span><span class="pn">.</span><span class="id">constrainAll</span> <span class="pn">[</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">present</span><span class="pn">;</span> <span class="id">Constraint</span><span class="pn">.</span><span class="id">trimmed</span> <span class="pn">]</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/SchemaApi.fs#L79-79)
