---
title: "Validation.Diagnostics.empty"
linkTitle: "empty"
weight: 2100
type: docs
---

Creates an empty diagnostics graph with no errors.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Diagnostics.empty&#32;<span></span></span></code>
</div>

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-validation-diagnostics-1.html">Diagnostics</a>&lt;'error&gt;</span></code> | An empty <a href="https://learn.microsoft.com/dotnet/api/axial.diagnostics-1">Diagnostics</a>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">d</span> <span class="o">=</span> <span class="id">Diagnostics</span><span class="pn">.</span><span class="id">empty</span><span class="pn">&lt;</span><span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">string</span><span class="pn">&gt;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val d: obj</div>
<div popover class="fsdocs-tip" id="fs2">Multiple items<br />val string: value: &#39;T -&gt; string<br /><br />--------------------<br />type string = System.String</div>
