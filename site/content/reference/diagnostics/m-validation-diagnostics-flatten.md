---
title: "Validation.Diagnostics.flatten"
linkTitle: "flatten"
weight: 2104
type: docs
---

Flattens the structured diagnostics graph into a linear list of diagnostics.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Diagnostics.flatten&#32;<span>graph</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `graph` | <code><span><a href="/reference/Axial/axial-validation-diagnostics-1.html">Diagnostics</a>&lt;'error&gt;</span></code> | The <a href="https://learn.microsoft.com/dotnet/api/axial.diagnostics-1">Diagnostics</a> to flatten. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><span><a href="/reference/Axial/axial-validation-diagnostic-1.html">Diagnostic</a>&lt;'error&gt;</span>&#32;list</span></code> | A list of type <a href="https://learn.microsoft.com/dotnet/api/axial.diagnostic-1">Diagnostic</a> list. |

## Remarks


 During flattening, child paths are accumulated from the root down into each emitted diagnostic.
 The tree itself stores only local errors and child branches, while <a href="https://learn.microsoft.com/dotnet/api/axial.diagnostic-1">Diagnostic</a>
 is reserved for reporting output.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">d</span> <span class="o">=</span> <span class="id">Diagnostics</span><span class="pn">.</span><span class="id">singleton</span> <span class="s">&quot;fail&quot;</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">flat</span> <span class="o">=</span> <span class="id">Diagnostics</span><span class="pn">.</span><span class="id">flatten</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="id">d</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val d: obj</div>
<div popover class="fsdocs-tip" id="fs2">val flat: obj</div>
