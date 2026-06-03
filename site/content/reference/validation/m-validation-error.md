---
title: "Validation.error"
linkTitle: "error"
weight: 2102
type: docs
---

Creates a failing validation result with the provided diagnostics.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.error&#32;<span>diagnostics</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `diagnostics` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-diagnostics-1.html">Diagnostics</a>&lt;'error&gt;</span></code> | The <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-diagnostics-1.html">Diagnostics</a> graph. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-validation-2.html">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | A failing <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-validation-2.html">Validation</a>. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">v</span> <span class="o">=</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">error</span> <span class="pn">(</span><span class="id">Diagnostics</span><span class="pn">.</span><span class="id">singleton</span> <span class="s">&quot;Something went wrong&quot;</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val v: obj</div>
