---
title: "Constraint.Renderer.ofCurrentCulture"
linkTitle: "ofCurrentCulture"
weight: 2910
type: docs
---

A renderer that reads the ambient cultures at each render rather than capturing them.

## Signature

<div class="fsdocs-usage">
<code><span>Constraint.Renderer.ofCurrentCulture&#32;<span>resources</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `resources` | <code><a href="https://learn.microsoft.com/dotnet/api/system.resources.resourcemanager">ResourceManager</a></code> |  |

## Returns

| Type | Description |
| --- | --- |
| <code><a href="t-constraint-renderer.md">Renderer</a></code> |  |

## Remarks

<code>CurrentUICulture</code> drives lookup and plural selection; <code>CurrentCulture</code> drives operand
 formatting. Both are read per render, so one renderer registered as a singleton follows a per-request
 culture. This is the one place ambient culture enters Axial: constraint execution stays effect-free.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"><span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">renderer</span> <span class="o">=</span> <span class="id">Renderer</span><span class="pn">.</span><span class="id">ofCurrentCulture</span> <span class="id">resources</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val renderer: obj</div>




[Source](https://github.com/adz/Axial/blob/main/src/Axial.Constraint/Renderer.fs#L579-579)
