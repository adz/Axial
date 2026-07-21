---
title: "Validation.Diagnostics.singleton"
linkTitle: "singleton"
weight: 2101
type: docs
---

Creates a diagnostics graph containing exactly one error at the root.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.Diagnostics.singleton&#32;<span>error</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `error` | <code>'error</code> | The application error to store at the root. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="t-validation-diagnostics.md">Diagnostics</a>&lt;'error&gt;</span></code> | A <a href="https://learn.microsoft.com/dotnet/api/axial.diagnostics-1">Diagnostics</a> with a single error. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">d</span> <span class="o">=</span> <span class="id">Diagnostics</span><span class="pn">.</span><span class="id">singleton</span> <span class="s">&quot;Something failed&quot;</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val d: obj</div>
