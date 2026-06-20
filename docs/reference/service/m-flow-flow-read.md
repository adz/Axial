---
title: "Flow.Flow.read"
linkTitle: "read"
weight: 2200
---

Projects one value from the current environment.

## Signature

<div class="fsdocs-usage">
<code><span>Flow.Flow.read&#32;<span>projection</span></span></code>
</div>

## Parameters

| Name | Type | Description |
| --- | --- | --- |
| `projection` | <code><span>'env&#32;->&#32;'value</span></code> | A function that extracts a value from the environment. |

## Returns

| Type | Description |
| --- | --- |
| <code><span><a href="/reference/Axial/axial-flow-flow-3.html">Flow</a>&lt;<span>'env,&#32;'error,&#32;'value</span>&gt;</span></code> | A <a href="https://learn.microsoft.com/dotnet/api/axial.flow-3">Flow</a> containing the projected value. |

## Remarks


 This is the primary way to access app dependencies, configuration, or request metadata stored
 in <code>env</code>. The projection runs only when the flow is executed, so constructing the flow is
 still pure and side-effect free. Prefer small projections over passing a large environment
 deeper into reusable helpers.


## Examples

<pre class="fssnip highlighted"><code lang="fsharp"> <span class="k">let</span> <span data-fsdocs-tip="fs1" data-fsdocs-tip-unique="1" class="id">myFlow</span> <span class="o">=</span> <span class="id">Flow</span><span class="pn">.</span><span class="id">read</span> <span class="pn">(</span><span class="k">fun</span> <span class="id">env</span> <span class="k">-&gt;</span> <span class="id">env</span><span class="pn">)</span>
</code></pre>
<div popover class="fsdocs-tip" id="fs1">val myFlow: obj</div>
