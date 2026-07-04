---
title: "Validation.Validation.ok"
linkTitle: "ok"
weight: 2101
---

Creates a successful validation result.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Validation.ok&#32;<span>value</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `value` | <code>'value</code> | The success value of type <code>&#39;value</code>. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-validation-validation.md">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | A successful <a href="https://learn.microsoft.com/dotnet/api/axial.validation-2">Validation</a>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">v</span> <span class="o">=</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="n">5</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val v: obj</div>
