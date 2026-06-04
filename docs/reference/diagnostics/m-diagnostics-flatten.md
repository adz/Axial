---
title: "Diagnostics.flatten"
linkTitle: "flatten"
weight: 2104
---

Flattens the structured diagnostics graph into a linear list of diagnostics.

## Signature

<div class="fsdocs-usage">
<code><span>Diagnostics.flatten&#32;<span>graph</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `graph` | <code><span><a href="t-diagnostics.md">Diagnostics</a>&lt;'error&gt;</span></code> | The <a href="t-diagnostics.md">Diagnostics</a> to flatten. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><span><a href="t-diagnostic.md">Diagnostic</a>&lt;'error&gt;</span>&#32;list</span></code> | A list of type <a href="t-diagnostic.md">Diagnostic</a> list. |

## Remarks


 During flattening, child paths are accumulated from the root down into each emitted diagnostic.
 The tree itself stores only local errors and child branches, while <a href="t-diagnostic.md">Diagnostic</a>
 is reserved for reporting output.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">d</span> <span class="o">=</span> <span class="id">Diagnostics</span><span class="pn">.</span><span class="id">singleton</span> <span class="s">&quot;fail&quot;</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">flat</span> <span class="o">=</span> <span class="id">Diagnostics</span><span class="pn">.</span><span class="id">flatten</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="3" class="id">d</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val d: obj</div>
<div popover class="fsdocs-tip" id="fs2">val flat: obj</div>
