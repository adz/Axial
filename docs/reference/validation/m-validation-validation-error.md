---
title: "Validation.Validation.error"
linkTitle: "error"
weight: 2102
---

Creates a failing validation result with the provided diagnostics.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Validation.error&#32;<span>diagnostics</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `diagnostics` | <code><span><a href="../diagnostics/t-validation-diagnostics.md">Diagnostics</a>&lt;'error&gt;</span></code> | The <a href="https://learn.microsoft.com/dotnet/api/axial.diagnostics-1">Diagnostics</a> graph. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | A failing <a href="https://learn.microsoft.com/dotnet/api/axial.validation-2">Validation</a>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">v</span> <span class="o">=</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">error</span> <span class="pn">(</span><span class="id">Diagnostics</span><span class="pn">.</span><span class="id">singleton</span> <span class="s">&quot;Something went wrong&quot;</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val v: obj</div>
