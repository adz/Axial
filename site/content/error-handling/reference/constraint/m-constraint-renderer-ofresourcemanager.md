---
title: "Constraint.Renderer.ofResourceManager"
linkTitle: "ofResourceManager"
weight: 2908
type: docs
---

A renderer backed by a .NET resource manager, using one culture for everything.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Renderer.ofResourceManager&#32;<span>resources&#32;culture</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `resources` | <code><a href="https://learn.microsoft.com/dotnet/api/system.resources.resourcemanager">ResourceManager</a></code> |  |
| `culture` | <code><a href="https://learn.microsoft.com/dotnet/api/system.globalization.cultureinfo">CultureInfo</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Remarks

The culture drives resource lookup, ordinary plural selection, and number and date formatting.

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">renderer</span> <span class="o">=</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">ofResourceManager</span> <span class="id">resources</span> <span class="pn">(</span><span class="id">CultureInfo</span> <span class="s">&quot;fr-FR&quot;</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val renderer: obj</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L553-553)
