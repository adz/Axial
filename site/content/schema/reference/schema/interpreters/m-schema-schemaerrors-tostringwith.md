---
title: "Schema.SchemaErrors.toStringWith"
linkTitle: "toStringWith"
weight: 2216
type: docs
---

Renders one localized line per failure.

## Signature

<div class="fsdocs-usage">
<code><span>Schema.SchemaErrors.toStringWith&#32;<span>renderer&#32;errors</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `renderer` | <code><a href="../../../../error-handling/reference/constraint/t-constraint-renderer.md">Renderer</a></code> |  |
| `errors` | <code><a href="t-schema-schemaerrors.md">SchemaErrors</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code>string</code> |  |

## Remarks

The localized counterpart of <code>toString</code>, using full messages so each line stands alone.

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">errors</span> <span class="o">|&gt;</span> <span class="id">SchemaErrors</span><span class="pn">.</span><span class="id">toStringWith</span> <span class="pn">(</span><span class="id">renderer</span> <span class="o">|&gt;</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">context</span> <span class="s">&quot;signup&quot;</span><span class="pn">)</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Schema/SchemaErrors.fs#L179-179)
