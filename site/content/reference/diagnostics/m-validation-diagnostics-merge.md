---
title: "Validation.Diagnostics.merge"
linkTitle: "merge"
weight: 2102
type: docs
---

Recursively merges two diagnostics graphs, combining shared branches and local errors.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Diagnostics.merge&#32;<span>left&#32;right</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `left` | <code><span><a href="/reference/Axial/axial-validation-diagnostics-1.html">Diagnostics</a>&lt;'error&gt;</span></code> | The first graph of type <a href="https://learn.microsoft.com/dotnet/api/axial.diagnostics-1">Diagnostics</a>. |
| `right` | <code><span><a href="/reference/Axial/axial-validation-diagnostics-1.html">Diagnostics</a>&lt;'error&gt;</span></code> | The second graph of type <a href="https://learn.microsoft.com/dotnet/api/axial.diagnostics-1">Diagnostics</a>. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-validation-diagnostics-1.html">Diagnostics</a>&lt;'error&gt;</span></code> | A new <a href="https://learn.microsoft.com/dotnet/api/axial.diagnostics-1">Diagnostics</a> containing the union of both inputs. |

## Remarks


 This is the core operation for applicative validation. It ensures that errors from sibling
 fields are collected together into a single structured graph.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">d1</span> <span class="o">=</span> <span class="id">Diagnostics</span><span class="pn">.</span><span class="id">singleton</span> <span class="s">&quot;Error 1&quot;</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="2" class="id">d2</span> <span class="o">=</span> <span class="id">Diagnostics</span><span class="pn">.</span><span class="id">singleton</span> <span class="s">&quot;Error 2&quot;</span>
 <span class="k">let</span> <span data-fsdocs-tip="fs3" data-fsdocs-tip-unique="3" class="id">combined</span> <span class="o">=</span> <span class="id">Diagnostics</span><span class="pn">.</span><span class="id">merge</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="4" class="id">d1</span> <span data-fsdocs-tip="fs2" data-fsdocs-tip-unique="5" class="id">d2</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val d1: obj</div>
<div popover class="fsdocs-tip" id="fs2">val d2: obj</div>
<div popover class="fsdocs-tip" id="fs3">val combined: obj</div>
