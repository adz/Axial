---
title: "Constraint.Renderer.ofResourceManagerWithCultures"
linkTitle: "ofResourceManagerWithCultures"
weight: 2909
type: docs
---

A renderer that looks messages up in one culture and formats operands in another.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Renderer.ofResourceManagerWithCultures&#32;<span>resources&#32;uiCulture&#32;valueCulture</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `resources` | <code><a href="https://learn.microsoft.com/dotnet/api/system.resources.resourcemanager">ResourceManager</a></code> |  |
| `uiCulture` | <code><a href="https://learn.microsoft.com/dotnet/api/system.globalization.cultureinfo">CultureInfo</a></code> |  |
| `valueCulture` | <code><a href="https://learn.microsoft.com/dotnet/api/system.globalization.cultureinfo">CultureInfo</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Remarks


 The split a UI needs when the interface language and the reader&#39;s number and date conventions differ —
 English text with German decimal separators, for instance.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="id">Renderer</span><span class="pn">.</span><span class="id">ofResourceManagerWithCultures</span> <span class="id">resources</span> <span class="pn">(</span><span class="id">CultureInfo</span> <span class="s">&quot;en&quot;</span><span class="pn">)</span> <span class="pn">(</span><span class="id">CultureInfo</span> <span class="s">&quot;de-DE&quot;</span><span class="pn">)</span>
</code></pre>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L563-563)
