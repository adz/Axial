---
title: "Validation.Diagnostics.toString"
linkTitle: "toString"
weight: 2103
type: docs
---

Renders a diagnostics graph in a YAML-like layout for display.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Diagnostics.toString&#32;<span>graph</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `graph` | <code><span><a href="/reference/Axial/axial-validation-diagnostics-1.html">Diagnostics</a>&lt;'error&gt;</span></code> | The diagnostics graph to render. |

## Returns

| Type | Description |
| --- | --- |
| <code>string</code> | A formatted string representation of the graph. |

## Remarks


 This is intended for human-readable output. Empty sections are omitted, and children are
 shown directly under their branch labels at the same indentation level as errors. Errors
 render as YAML-style bullet items without an `Errors:` key. Use
 <a href="https://learn.microsoft.com/dotnet/api/axial.diagnostics.flatten">flatten</a> when you need path-bearing diagnostics for
 reporting or assertions.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">d</span> <span class="o">=</span> <span class="id">Diagnostics</span><span class="pn">.</span><span class="id">singleton</span> <span class="s">&quot;fail&quot;</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">s</span> <span class="o">=</span> <span class="id">Diagnostics</span><span class="pn">.</span><span class="id">toString</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="id">d</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val d: obj</div>
<div popover class="fsdocs-tip" id="fs2">val s: obj</div>
