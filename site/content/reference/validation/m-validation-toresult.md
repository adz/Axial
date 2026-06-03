---
title: "Validation.toResult"
linkTitle: "toResult"
weight: 2100
type: docs
---

Converts a <a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-validation-2.html">Validation</a> into a standard <a href="https://learn.microsoft.com/dotnet/api/system.result-2">Result</a>.

## Signature

<div class="fsdocs-usage">
<code><span>Validation.toResult&#32;<span>validation</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `validation` | <code><span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-validation-2.html">Validation</a>&lt;<span>'value,&#32;'error</span>&gt;</span></code> | The validation to convert. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpresult-2">Result</a>&lt;<span>'value,&#32;<span><a href="https://adz.github.io/FsFlow/reference/FsFlow/fsflow-diagnostics-1.html">Diagnostics</a>&lt;'error&gt;</span></span>&gt;</span></code> | A result containing either the success value or the full diagnostics graph. |

## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">res</span> <span class="o">=</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">ok</span> <span class="n">5</span> <span class="o">|&gt;</span> <span class="id">Validation</span><span class="pn">.</span><span class="id">toResult</span> <span class="c">// Ok 5</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val res: obj</div>
